using Microsoft.Data.SqlClient;
using System.Data;

namespace Seive2.Services;

public sealed class LargestPrimeTracker
{
    private readonly string _connectionString;
    private readonly ILogger<LargestPrimeTracker> _logger;

    public LargestPrimeTracker(IConfiguration configuration, ILogger<LargestPrimeTracker> logger)
    {
        _connectionString = configuration.GetConnectionString("WorkDatabase")
            ?? throw new InvalidOperationException("ConnectionStrings:WorkDatabase must be configured.");
        _logger = logger;
    }

    public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT CASE WHEN OBJECT_ID(N'dbo.LargestPrime', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 1)
        {
            throw new InvalidOperationException("Largest-prime table is missing. Apply infra/schema.sql before starting the API.");
        }

        _logger.LogInformation("Largest-prime tracking table is ready.");
    }

    public async Task<long> GetLargestPrimeAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COALESCE(MAX(Value), 0) FROM dbo.LargestPrime;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    public async Task UpdateLargestPrimeAsync(long newPrime, CancellationToken cancellationToken = default)
    {
        if (newPrime <= 0)
        {
            return;
        }

        const string sql = "UPDATE dbo.LargestPrime SET Value = @NewValue WHERE Value < @NewValue;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@NewValue", SqlDbType.BigInt).Value = newPrime;
        var rowsUpdated = await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogDebug("Updated largest-prime value {Prime}; rows updated: {RowsUpdated}.", newPrime, rowsUpdated);
    }

    public async Task UpdateFromRangeAsync(long startNumber, long endNumber, long primesFoundInRange, CancellationToken cancellationToken = default)
    {
        if (primesFoundInRange > 0)
        {
            await UpdateLargestPrimeAsync(endNumber, cancellationToken);
        }
    }
}
