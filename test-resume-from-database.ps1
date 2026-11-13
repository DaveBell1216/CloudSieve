# Test Resume-from-Database Feature

param(
	[string]$ApiUrl = "http://192.168.1.9:5150",
	[long]$TestPrimeValue = 1000000
)

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Testing Resume-from-Database Feature" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Helper function
filter Format-Number {
	[System.String]::Format("{0:N0}", $_)
}

Write-Host "[TEST SETUP]" -ForegroundColor Yellow
Write-Host "This test requires direct SQL Server access to Bridge\Primes database" -ForegroundColor Gray
Write-Host ""

# Test 1: Read current largest prime from database
Write-Host "[1/4] Reading current largest prime from database..." -ForegroundColor Yellow
try {
	$response = Invoke-RestMethod -Uri "$ApiUrl/api/work/largest-prime" -Method Get
	$currentLargest = $response.largestPrime
	Write-Host "✅ Current largest prime in database: $($currentLargest | Format-Number)" -ForegroundColor Green
} catch {
	Write-Host "❌ Failed to read largest prime: $_" -ForegroundColor Red
	Write-Host "   Make sure API is running at: $ApiUrl" -ForegroundColor Yellow
	exit 1
}
Write-Host ""

# Test 2: Check current job status
Write-Host "[2/4] Checking current job status..." -ForegroundColor Yellow
try {
	$status = Invoke-RestMethod -Uri "$ApiUrl/api/job/status" -Method Get
	Write-Host "✅ Job is running:" -ForegroundColor Green
	Write-Host "   Job ID: $($status.jobId)" -ForegroundColor White
	Write-Host "   Status: $($status.status)" -ForegroundColor White
	Write-Host "   Max Number: $($status.maxNumber | Format-Number)" -ForegroundColor White
	Write-Host "   Progress: $($status.completedSegments)/$($status.totalSegments) segments ($($status.progressPercentage.ToString('F2'))%)" -ForegroundColor White

	# Check if job started from expected point
	Write-Host "" -ForegroundColor White
	Write-Host "   ℹ️  NOTE: To verify resume behavior, you need to:" -ForegroundColor Cyan
	Write-Host "      1. Note the current largest prime: $($currentLargest | Format-Number)" -ForegroundColor Cyan
	Write-Host "      2. Restart the API service" -ForegroundColor Cyan
	Write-Host "      3. Check API logs for resume message" -ForegroundColor Cyan
	Write-Host "      4. Request a work segment (should start after $($currentLargest | Format-Number))" -ForegroundColor Cyan
} catch {
	Write-Host "❌ Failed to get job status: $_" -ForegroundColor Red
}
Write-Host ""

# Test 3: Get a work segment to see what range is being assigned
Write-Host "[3/4] Requesting a work segment (to see current assignment range)..." -ForegroundColor Yellow
try {
	$workRequest = @{
		workerId = "test-resume-check"
		workerName = "Resume Test"
		batchSize = 1
	} | ConvertTo-Json

	$segment = Invoke-RestMethod -Uri "$ApiUrl/api/work/next" -Method Post -Body $workRequest -ContentType "application/json"

	if ($segment -and $segment.startNumber) {
		Write-Host "✅ Received segment:" -ForegroundColor Green
		Write-Host "   Segment ID: $($segment.segmentId)" -ForegroundColor White
		Write-Host "   Start: $($segment.startNumber | Format-Number)" -ForegroundColor White
		Write-Host "   End: $($segment.endNumber | Format-Number)" -ForegroundColor White

		Write-Host "" -ForegroundColor White
		if ($segment.startNumber -gt $currentLargest) {
			Write-Host "   ✅ Segment starts AFTER current largest prime ($($currentLargest | Format-Number))" -ForegroundColor Green
			Write-Host "      This indicates the job is processing new ranges (expected behavior)" -ForegroundColor Green
		} else {
			Write-Host "   ℹ️  Segment starts BEFORE current largest prime ($($currentLargest | Format-Number))" -ForegroundColor Cyan
			Write-Host "      This could mean:" -ForegroundColor Yellow
			Write-Host "      - Job was started before resume feature was implemented" -ForegroundColor Yellow
			Write-Host "      - Database value hasn't been updated yet" -ForegroundColor Yellow
			Write-Host "      - This is expected if you just restarted with no progress" -ForegroundColor Yellow
		}
	} else {
		Write-Host "⚠️  No segments available (job may be complete or no job running)" -ForegroundColor Yellow
	}
} catch {
	if ($_.Exception.Response.StatusCode -eq 204) {
		Write-Host "⚠️  No work available (NoContent response)" -ForegroundColor Yellow
		Write-Host "   This means all segments are assigned or job is complete" -ForegroundColor Gray
	} else {
		Write-Host "❌ Failed to get work segment: $_" -ForegroundColor Red
	}
}
Write-Host ""

# Test 4: Instructions for manual verification
Write-Host "[4/4] Manual Verification Steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "To fully test the resume-from-database feature:" -ForegroundColor White
Write-Host ""
Write-Host "Step 1: Note the current largest prime" -ForegroundColor Cyan
Write-Host "   Current value: $($currentLargest | Format-Number)" -ForegroundColor White
Write-Host ""
Write-Host "Step 2: Restart the API" -ForegroundColor Cyan
Write-Host "   PowerShell: Restart-WebAppPool -Name 'PrimeSieveAPI'" -ForegroundColor Gray
Write-Host "   Or manually restart the IIS app pool / service" -ForegroundColor Gray
Write-Host ""
Write-Host "Step 3: Check API startup logs" -ForegroundColor Cyan
Write-Host "   Look for one of these messages:" -ForegroundColor White
Write-Host "   - '📊 Resuming from database: Starting after largest prime X'" -ForegroundColor Green
Write-Host "   - '🆕 Starting fresh from 2 (no previous progress in database)'" -ForegroundColor Green
Write-Host ""
Write-Host "Step 4: Request a segment and verify start number" -ForegroundColor Cyan
Write-Host "   Run this script again after restart" -ForegroundColor Gray
Write-Host "   The segment startNumber should be > $($currentLargest | Format-Number)" -ForegroundColor White
Write-Host ""

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Automated Testing Complete!" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "For SQL-level verification, run on SQL Server:" -ForegroundColor Gray
Write-Host "  USE Primes;" -ForegroundColor DarkGray
Write-Host "  SELECT Value FROM LargestPrime;" -ForegroundColor DarkGray
Write-Host ""
