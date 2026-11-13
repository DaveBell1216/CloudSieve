# Test SQL Server Largest Prime Tracking

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Testing Largest Prime SQL Tracking" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

$apiUrl = "http://192.168.1.9:5150"

# Test 1: Check API health
Write-Host "[1/3] Checking API health..." -ForegroundColor Yellow
try {
	$health = Invoke-RestMethod -Uri "$apiUrl/api/job/health" -Method Get
	Write-Host "✅ API is healthy" -ForegroundColor Green
} catch {
	Write-Host "❌ API health check failed: $_" -ForegroundColor Red
	exit 1
}
Write-Host ""

# Test 2: Get current largest prime
Write-Host "[2/3] Getting current largest prime from database..." -ForegroundColor Yellow
try {
	$response = Invoke-RestMethod -Uri "$apiUrl/api/work/largest-prime" -Method Get
	Write-Host "✅ Current largest prime: $($response.largestPrime | Format-Number)" -ForegroundColor Green

	if ($response.largestPrime -eq 0) {
		Write-Host "   ℹ️  No primes found yet (initial value)" -ForegroundColor Cyan
	}
} catch {
	Write-Host "❌ Failed to get largest prime: $_" -ForegroundColor Red
}
Write-Host ""

# Test 3: Get job status to see if work is being processed
Write-Host "[3/3] Checking job status..." -ForegroundColor Yellow
try {
	$status = Invoke-RestMethod -Uri "$apiUrl/api/job/status" -Method Get
	Write-Host "✅ Job Status:" -ForegroundColor Green
	Write-Host "   Job ID: $($status.jobId)" -ForegroundColor White
	Write-Host "   Status: $($status.status)" -ForegroundColor White
	Write-Host "   Progress: $($status.completedSegments)/$($status.totalSegments) segments ($($status.progressPercentage.ToString('F2'))%)" -ForegroundColor White
	Write-Host "   Total Primes Found: $($status.totalPrimesFound | Format-Number)" -ForegroundColor White

	if ($status.completedSegments -gt 0) {
		Write-Host "   ℹ️  Workers have completed segments - check largest prime!" -ForegroundColor Cyan
	}
} catch {
	Write-Host "❌ Failed to get job status: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Testing Complete!" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Helper function for number formatting
filter Format-Number {
	[System.String]::Format("{0:N0}", $_)
}
