IF OBJECT_ID(N'dbo.SieveJobs', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.SieveJobs (
		JobId uniqueidentifier NOT NULL CONSTRAINT PK_SieveJobs PRIMARY KEY,
		StartNumber bigint NOT NULL,
		NextNumber bigint NULL,
		MaxNumber bigint NOT NULL,
		SegmentSize bigint NOT NULL,
		TotalSegments int NOT NULL,
		CompletedSegments int NOT NULL CONSTRAINT DF_SieveJobs_Completed DEFAULT 0,
		TotalPrimesFound bigint NOT NULL CONSTRAINT DF_SieveJobs_Primes DEFAULT 0,
		Status int NOT NULL,
		StartedAt datetime2 NOT NULL,
		CompletedAt datetime2 NULL
	);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SieveJobs') AND name = N'UX_SieveJobs_OneRunning')
BEGIN
	CREATE UNIQUE INDEX UX_SieveJobs_OneRunning ON dbo.SieveJobs(Status) WHERE Status = 1;
END;

IF OBJECT_ID(N'dbo.SieveSegments', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.SieveSegments (
		SegmentId uniqueidentifier NOT NULL CONSTRAINT PK_SieveSegments PRIMARY KEY,
		AssignmentId uniqueidentifier NULL,
		JobId uniqueidentifier NOT NULL,
		StartNumber bigint NOT NULL,
		EndNumber bigint NOT NULL,
		Status int NOT NULL,
		WorkerId nvarchar(200) NULL,
		AssignedAt datetime2 NULL,
		PrimesFound bigint NULL,
		LargestPrimeFound bigint NULL,
		CONSTRAINT FK_SieveSegments_SieveJobs FOREIGN KEY (JobId) REFERENCES dbo.SieveJobs(JobId)
	);
END;

IF COL_LENGTH(N'dbo.SieveSegments', N'AssignmentId') IS NULL
BEGIN
    ALTER TABLE dbo.SieveSegments ADD AssignmentId uniqueidentifier NULL;
    UPDATE dbo.SieveSegments SET Status = 0, WorkerId = NULL, AssignedAt = NULL WHERE Status = 1;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SieveSegments') AND name = N'IX_SieveSegments_Claim')
BEGIN
	CREATE INDEX IX_SieveSegments_Claim ON dbo.SieveSegments(JobId, Status, StartNumber) INCLUDE (EndNumber, SegmentId);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SieveSegments') AND name = N'IX_SieveSegments_Expiry')
BEGIN
	CREATE INDEX IX_SieveSegments_Expiry ON dbo.SieveSegments(JobId, Status, AssignedAt) INCLUDE (SegmentId);
END;

IF OBJECT_ID(N'dbo.LargestPrime', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.LargestPrime (Value bigint NOT NULL);
END;

DECLARE @ExistingLargestPrime bigint = $(ExistingLargestPrime);
IF @ExistingLargestPrime < 0
BEGIN
	THROW 50000, 'ExistingLargestPrime must be non-negative.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.LargestPrime)
BEGIN
	INSERT INTO dbo.LargestPrime (Value) VALUES (@ExistingLargestPrime);
END;
ELSE
BEGIN
	UPDATE dbo.LargestPrime SET Value = @ExistingLargestPrime WHERE Value < @ExistingLargestPrime;
END;

-- Run this script as the Entra SQL administrator after replacing the SQLCMD
-- variables with the API identity name and the migrated watermark value.
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(ApiIdentityName)')
BEGIN
	CREATE USER [$(ApiIdentityName)] FROM EXTERNAL PROVIDER;
END;
GRANT SELECT, INSERT, UPDATE ON dbo.SieveJobs TO [$(ApiIdentityName)];
GRANT SELECT, INSERT, UPDATE ON dbo.SieveSegments TO [$(ApiIdentityName)];
GRANT SELECT, INSERT, UPDATE ON dbo.LargestPrime TO [$(ApiIdentityName)];