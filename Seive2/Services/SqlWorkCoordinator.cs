using Microsoft.Data.SqlClient;
using Seive2.Shared.Models;
using System.Data;

namespace Seive2.Services;

public sealed class SqlWorkCoordinator
{
    private readonly string _connectionString;
    private readonly ILogger<SqlWorkCoordinator> _logger;

    public SqlWorkCoordinator(IConfiguration configuration, ILogger<SqlWorkCoordinator> logger)
    {
        _connectionString = configuration.GetConnectionString("WorkDatabase")
            ?? throw new InvalidOperationException("ConnectionStrings:WorkDatabase must be configured.");
        _logger = logger;
    }

    public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN OBJECT_ID(N'dbo.SieveJobs', N'U') IS NOT NULL
                AND OBJECT_ID(N'dbo.SieveSegments', N'U') IS NOT NULL
                AND COL_LENGTH(N'dbo.SieveSegments', N'AssignmentId') IS NOT NULL
                AND OBJECT_ID(N'dbo.LargestPrime', N'U') IS NOT NULL
                AND EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SieveJobs')
                    AND name = N'UX_SieveJobs_OneRunning' AND is_unique = 1 AND is_disabled = 0)
                THEN 1 ELSE 0 END;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        var schemaReady = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
        if (!schemaReady)
        {
            throw new InvalidOperationException("Sieve database schema is missing. Apply infra/schema.sql before starting the API.");
        }

        _logger.LogInformation("Sieve database schema is ready.");
    }

    public async Task<SieveJob> StartJobAsync(long startNumber, long maxNumber, long segmentSize, CancellationToken cancellationToken = default)
    {
        startNumber = Math.Max(2, startNumber);
        if (maxNumber < startNumber || segmentSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxNumber), "The range and segment size must be positive and ordered.");
        }

        var segmentCount = ((maxNumber - startNumber) / segmentSize) + 1;
        if (segmentCount > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentSize), "This job has more segments than the database model can track.");
        }

        var job = new SieveJob
        {
            JobId = Guid.NewGuid(),
            MaxNumber = maxNumber,
            SegmentSize = segmentSize,
            StartedAt = DateTime.UtcNow,
            Status = JobStatus.Running,
            TotalSegments = (int)segmentCount
        };

        const string sql = """
            INSERT INTO dbo.SieveJobs (JobId, StartNumber, NextNumber, MaxNumber, SegmentSize, TotalSegments, Status, StartedAt)
            VALUES (@JobId, @StartNumber, @StartNumber, @MaxNumber, @SegmentSize, @TotalSegments, @Status, @StartedAt);
            """;

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = job.JobId;
            command.Parameters.Add("@StartNumber", SqlDbType.BigInt).Value = startNumber;
            command.Parameters.Add("@MaxNumber", SqlDbType.BigInt).Value = maxNumber;
            command.Parameters.Add("@SegmentSize", SqlDbType.BigInt).Value = segmentSize;
            command.Parameters.Add("@TotalSegments", SqlDbType.Int).Value = job.TotalSegments;
            command.Parameters.Add("@Status", SqlDbType.Int).Value = (int)job.Status;
            command.Parameters.Add("@StartedAt", SqlDbType.DateTime2).Value = job.StartedAt;
            await command.ExecuteNonQueryAsync(cancellationToken);
            return job;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new InvalidOperationException("A sieve job is already running.", ex);
        }
    }

    public async Task<List<WorkSegment>> GetNextWorkSegmentsAsync(string workerId, int batchSize, CancellationToken cancellationToken = default)
    {
        var segments = new List<WorkSegment>(batchSize);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            Guid jobId = default;
            long? nextNumber = null;
            long maxNumber = 0;
            long segmentSize = 0;
            bool hasRunningJob;
            const string jobSql = "SELECT TOP (1) JobId, NextNumber, MaxNumber, SegmentSize FROM dbo.SieveJobs WITH (UPDLOCK, HOLDLOCK) WHERE Status = @Running ORDER BY StartedAt DESC;";
            await using (var jobCommand = new SqlCommand(jobSql, connection, transaction))
            {
                jobCommand.Parameters.Add("@Running", SqlDbType.Int).Value = (int)JobStatus.Running;
                await using var reader = await jobCommand.ExecuteReaderAsync(cancellationToken);
                hasRunningJob = await reader.ReadAsync(cancellationToken);
                if (hasRunningJob)
                {
                    jobId = reader.GetGuid(0);
                    nextNumber = reader.IsDBNull(1) ? null : reader.GetInt64(1);
                    maxNumber = reader.GetInt64(2);
                    segmentSize = reader.GetInt64(3);
                }
            }

            if (!hasRunningJob)
            {
                await transaction.CommitAsync(cancellationToken);
                return segments;
            }

            const string requeueSql = "UPDATE dbo.SieveSegments SET Status = @Pending, WorkerId = NULL, AssignedAt = NULL WHERE JobId = @JobId AND Status = @Assigned AND AssignedAt < DATEADD(minute, -10, SYSUTCDATETIME());";
            await using (var requeueCommand = new SqlCommand(requeueSql, connection, transaction))
            {
                requeueCommand.Parameters.Add("@Pending", SqlDbType.Int).Value = (int)SegmentStatus.Pending;
                requeueCommand.Parameters.Add("@Assigned", SqlDbType.Int).Value = (int)SegmentStatus.Assigned;
                requeueCommand.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;
                await requeueCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            const string claimSql = """
                ;WITH candidates AS (
                    SELECT TOP (@BatchSize) SegmentId
                    FROM dbo.SieveSegments WITH (UPDLOCK, ROWLOCK)
                    WHERE JobId = @JobId AND Status = @Pending
                    ORDER BY StartNumber
                )
                UPDATE segment
                SET Status = @Assigned, WorkerId = @WorkerId, AssignedAt = SYSUTCDATETIME(), AssignmentId = NEWID()
                OUTPUT inserted.SegmentId, inserted.StartNumber, inserted.EndNumber, inserted.AssignedAt, inserted.WorkerId, inserted.Status, inserted.AssignmentId
                FROM dbo.SieveSegments AS segment
                INNER JOIN candidates ON candidates.SegmentId = segment.SegmentId;
                """;
            await using (var claimCommand = new SqlCommand(claimSql, connection, transaction))
            {
                claimCommand.Parameters.Add("@BatchSize", SqlDbType.Int).Value = batchSize;
                claimCommand.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;
                claimCommand.Parameters.Add("@Pending", SqlDbType.Int).Value = (int)SegmentStatus.Pending;
                claimCommand.Parameters.Add("@Assigned", SqlDbType.Int).Value = (int)SegmentStatus.Assigned;
                claimCommand.Parameters.Add("@WorkerId", SqlDbType.NVarChar, 200).Value = workerId;
                await using var reader = await claimCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    segments.Add(ReadSegment(reader));
                }
            }

            while (segments.Count < batchSize && nextNumber.HasValue && nextNumber.Value <= maxNumber)
            {
                var start = nextNumber.Value;
                var end = Math.Min(start + segmentSize - 1, maxNumber);
                var segment = new WorkSegment
                {
                    SegmentId = Guid.NewGuid(),
                    AssignmentId = Guid.NewGuid(),
                    StartNumber = start,
                    EndNumber = end,
                    AssignedAt = DateTime.UtcNow,
                    WorkerId = workerId,
                    Status = SegmentStatus.Assigned
                };
                const string insertSql = "INSERT INTO dbo.SieveSegments (SegmentId, AssignmentId, JobId, StartNumber, EndNumber, Status, WorkerId, AssignedAt) VALUES (@SegmentId, @AssignmentId, @JobId, @StartNumber, @EndNumber, @Status, @WorkerId, SYSUTCDATETIME());";
                await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
                insertCommand.Parameters.Add("@SegmentId", SqlDbType.UniqueIdentifier).Value = segment.SegmentId;
                insertCommand.Parameters.Add("@AssignmentId", SqlDbType.UniqueIdentifier).Value = segment.AssignmentId;
                insertCommand.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;
                insertCommand.Parameters.Add("@StartNumber", SqlDbType.BigInt).Value = start;
                insertCommand.Parameters.Add("@EndNumber", SqlDbType.BigInt).Value = end;
                insertCommand.Parameters.Add("@Status", SqlDbType.Int).Value = (int)SegmentStatus.Assigned;
                insertCommand.Parameters.Add("@WorkerId", SqlDbType.NVarChar, 200).Value = workerId;
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                segments.Add(segment);
                nextNumber = end == maxNumber ? null : end + 1;
            }

            const string updateNextSql = "UPDATE dbo.SieveJobs SET NextNumber = @NextNumber WHERE JobId = @JobId;";
            await using (var updateCommand = new SqlCommand(updateNextSql, connection, transaction))
            {
                updateCommand.Parameters.Add("@NextNumber", SqlDbType.BigInt).Value = (object?)nextNumber ?? DBNull.Value;
                updateCommand.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;
                await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return segments;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> CompleteSegmentAsync(WorkResult result, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            const string jobIdSql = "SELECT JobId FROM dbo.SieveSegments WHERE SegmentId = @SegmentId;";
            Guid jobId;
            await using (var jobIdCommand = new SqlCommand(jobIdSql, connection, transaction))
            {
                jobIdCommand.Parameters.Add("@SegmentId", SqlDbType.UniqueIdentifier).Value = result.SegmentId;
                var value = await jobIdCommand.ExecuteScalarAsync(cancellationToken);
                if (value is not Guid foundJobId)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }
                jobId = foundJobId;
            }

            const string lockJobSql = "SELECT JobId FROM dbo.SieveJobs WITH (UPDLOCK, HOLDLOCK) WHERE JobId = @JobId;";
            await using (var lockJobCommand = new SqlCommand(lockJobSql, connection, transaction))
            {
                lockJobCommand.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;
                if (await lockJobCommand.ExecuteScalarAsync(cancellationToken) is null)
                {
                    throw new InvalidOperationException("The segment's job does not exist.");
                }
            }

            const string readSql = "SELECT JobId, StartNumber, EndNumber, Status, WorkerId, PrimesFound, LargestPrimeFound, AssignmentId FROM dbo.SieveSegments WITH (UPDLOCK, ROWLOCK) WHERE SegmentId = @SegmentId;";
            long startNumber = 0;
            long endNumber = 0;
            SegmentStatus status = default;
            string? assignedWorker = null;
            long? storedPrimesFound = null;
            long? storedLargestPrimeFound = null;
            Guid? assignmentId = null;
            bool segmentFound;
            await using (var readCommand = new SqlCommand(readSql, connection, transaction))
            {
                readCommand.Parameters.Add("@SegmentId", SqlDbType.UniqueIdentifier).Value = result.SegmentId;
                await using var reader = await readCommand.ExecuteReaderAsync(cancellationToken);
                segmentFound = await reader.ReadAsync(cancellationToken);
                if (segmentFound)
                {
                    jobId = reader.GetGuid(0);
                    startNumber = reader.GetInt64(1);
                    endNumber = reader.GetInt64(2);
                    status = (SegmentStatus)reader.GetInt32(3);
                    assignedWorker = reader.IsDBNull(4) ? null : reader.GetString(4);
                    storedPrimesFound = reader.IsDBNull(5) ? null : reader.GetInt64(5);
                    storedLargestPrimeFound = reader.IsDBNull(6) ? null : reader.GetInt64(6);
                    assignmentId = reader.IsDBNull(7) ? null : reader.GetGuid(7);
                }
            }

            if (!segmentFound)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            if (result.AssignmentId == Guid.Empty || assignmentId != result.AssignmentId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            if (status == SegmentStatus.Completed)
            {
                await transaction.CommitAsync(cancellationToken);
                return result.Success &&
                    string.Equals(assignedWorker, result.WorkerId, StringComparison.Ordinal) &&
                    storedPrimesFound == result.PrimesFound &&
                    storedLargestPrimeFound == result.LargestPrimeFound;
            }
            if (status != SegmentStatus.Assigned || !string.Equals(assignedWorker, result.WorkerId, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
            if (result.PrimesFound < 0 || result.PrimesFound > endNumber - startNumber + 1 || result.LargestPrimeFound < 0 ||
                (result.PrimesFound == 0 && result.LargestPrimeFound != 0) ||
                (result.PrimesFound > 0 && (result.LargestPrimeFound < startNumber || result.LargestPrimeFound > endNumber)))
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            if (result.Success)
            {
                const string completeSql = "UPDATE dbo.SieveSegments SET Status = @Completed, PrimesFound = @PrimesFound, LargestPrimeFound = @LargestPrimeFound WHERE SegmentId = @SegmentId; UPDATE dbo.SieveJobs SET CompletedSegments = CompletedSegments + 1, TotalPrimesFound = TotalPrimesFound + @PrimesFound, Status = CASE WHEN CompletedSegments + 1 = TotalSegments THEN @JobCompleted ELSE Status END, CompletedAt = CASE WHEN CompletedSegments + 1 = TotalSegments THEN SYSUTCDATETIME() ELSE CompletedAt END WHERE JobId = @JobId; UPDATE dbo.LargestPrime SET Value = @LargestPrimeFound WHERE Value < @LargestPrimeFound;";
                await using var completeCommand = new SqlCommand(completeSql, connection, transaction);
                completeCommand.Parameters.Add("@Completed", SqlDbType.Int).Value = (int)SegmentStatus.Completed;
                completeCommand.Parameters.Add("@PrimesFound", SqlDbType.BigInt).Value = result.PrimesFound;
                completeCommand.Parameters.Add("@LargestPrimeFound", SqlDbType.BigInt).Value = result.LargestPrimeFound;
                completeCommand.Parameters.Add("@SegmentId", SqlDbType.UniqueIdentifier).Value = result.SegmentId;
                completeCommand.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;
                completeCommand.Parameters.Add("@JobCompleted", SqlDbType.Int).Value = (int)JobStatus.Completed;
                await completeCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                const string retrySql = "UPDATE dbo.SieveSegments SET Status = @Pending, WorkerId = NULL, AssignedAt = NULL WHERE SegmentId = @SegmentId;";
                await using var retryCommand = new SqlCommand(retrySql, connection, transaction);
                retryCommand.Parameters.Add("@Pending", SqlDbType.Int).Value = (int)SegmentStatus.Pending;
                retryCommand.Parameters.Add("@SegmentId", SqlDbType.UniqueIdentifier).Value = result.SegmentId;
                await retryCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<JobStatusResponse?> GetJobStatusAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (1) j.JobId, j.Status, j.MaxNumber, j.TotalPrimesFound, j.TotalSegments,
                j.CompletedSegments, j.StartedAt, j.CompletedAt,
                (SELECT COUNT(*) FROM dbo.SieveSegments s WHERE s.JobId = j.JobId AND s.Status = @Assigned) AS AssignedSegments
            FROM dbo.SieveJobs j
            ORDER BY j.StartedAt DESC;
            """;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Assigned", SqlDbType.Int).Value = (int)SegmentStatus.Assigned;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var totalSegments = reader.GetInt32(4);
        var completedSegments = reader.GetInt32(5);
        var assignedSegments = reader.GetInt32(8);
        var startedAt = reader.GetDateTime(6);
        DateTime? completedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7);
        return new JobStatusResponse
        {
            JobId = reader.GetGuid(0),
            Status = (JobStatus)reader.GetInt32(1),
            MaxNumber = reader.GetInt64(2),
            TotalPrimesFound = reader.GetInt64(3),
            TotalSegments = totalSegments,
            CompletedSegments = completedSegments,
            AssignedSegments = assignedSegments,
            PendingSegments = totalSegments - completedSegments - assignedSegments,
            ProgressPercentage = totalSegments > 0 ? (double)completedSegments / totalSegments * 100 : 0,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ElapsedTime = (completedAt ?? DateTime.UtcNow) - startedAt
        };
    }

    private static WorkSegment ReadSegment(SqlDataReader reader) => new()
    {
        SegmentId = reader.GetGuid(0),
        StartNumber = reader.GetInt64(1),
        EndNumber = reader.GetInt64(2),
        AssignedAt = reader.GetDateTime(3),
        WorkerId = reader.GetString(4),
        Status = (SegmentStatus)reader.GetInt32(5),
        AssignmentId = reader.GetGuid(6)
    };
}
