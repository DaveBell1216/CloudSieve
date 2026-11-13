# Start Job on Server API
# Run this FROM YOUR DEVELOPMENT MACHINE after the API is running on 192.168.1.9

param(
	[string]$ServerUrl = "http://192.168.1.9:5150",
	[long]$MaxNumber = 100000000000,
	[long]$SegmentSize = 100000
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Start Sieve Job on Server" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Test connection
Write-Host "Testing connection to $ServerUrl..." -ForegroundColor Cyan
try {
	$health = Invoke-RestMethod -Uri "$ServerUrl/api/job/health" -TimeoutSec 5
	if ($health.status -eq "healthy") {
		Write-Host "✅ API is healthy" -ForegroundColor Green
	} else {
		Write-Host "⚠️  API responded but status is: $($health.status)" -ForegroundColor Yellow
	}
} catch {
	Write-Host "❌ Cannot connect to API at $ServerUrl" -ForegroundColor Red
	Write-Host "   Error: $_" -ForegroundColor Red
	Write-Host ""
	Write-Host "Make sure:" -ForegroundColor Yellow
	Write-Host "  1. API is running on the server (192.168.1.9)" -ForegroundColor Gray
	Write-Host "  2. Server firewall allows port 5150" -ForegroundColor Gray
	Write-Host "  3. Network connectivity between machines" -ForegroundColor Gray
	exit 1
}

# Start job
Write-Host ""
Write-Host "Starting job..." -ForegroundColor Cyan
Write-Host "  Max Number: $($MaxNumber.ToString('N0'))" -ForegroundColor White
Write-Host "  Segment Size: $($SegmentSize.ToString('N0'))" -ForegroundColor White
Write-Host ""

$body = @{
	maxNumber = $MaxNumber
	segmentSize = $SegmentSize
} | ConvertTo-Json

try {
	$result = Invoke-RestMethod -Uri "$ServerUrl/api/job/start" -Method Post -Body $body -ContentType "application/json"

	Write-Host "✅ Job Started Successfully!" -ForegroundColor Green
	Write-Host ""
	Write-Host "Job Details:" -ForegroundColor Cyan
	Write-Host "  Job ID: $($result.jobId)" -ForegroundColor White
	Write-Host "  Max Number: $($result.maxNumber.ToString('N0'))" -ForegroundColor White
	Write-Host "  Segment Size: $($result.segmentSize.ToString('N0'))" -ForegroundColor White
	Write-Host "  Total Segments: $($result.totalSegments.ToString('N0'))" -ForegroundColor White
	Write-Host "  Started At: $($result.startedAt)" -ForegroundColor White
	Write-Host ""

	# Calculate estimates
	$segmentsPerWorker = 25
	$workersNeeded = [Math]::Ceiling($result.totalSegments / $segmentsPerWorker)

	Write-Host "Estimated Work:" -ForegroundColor Cyan
	Write-Host "  Workers needed (25 seg each): $($workersNeeded.ToString('N0'))" -ForegroundColor White
	Write-Host ""

	Write-Host "To start workers from this machine:" -ForegroundColor Yellow
	Write-Host "  cd Seive2.Worker" -ForegroundColor Gray
	Write-Host "  dotnet run -- $ServerUrl" -ForegroundColor Gray
	Write-Host ""

	Write-Host "To check job status:" -ForegroundColor Yellow
	Write-Host "  Invoke-RestMethod -Uri '$ServerUrl/api/job/status' | ConvertTo-Json" -ForegroundColor Gray
	Write-Host ""

} catch {
	Write-Host "❌ Failed to start job" -ForegroundColor Red
	Write-Host "   Error: $_" -ForegroundColor Red
	exit 1
}

# Check status immediately
Write-Host "Current Status:" -ForegroundColor Cyan
try {
	$status = Invoke-RestMethod -Uri "$ServerUrl/api/job/status"
	Write-Host "  Status: $($status.status)" -ForegroundColor White
	Write-Host "  Completed: $($status.completedSegments.ToString('N0')) / $($status.totalSegments.ToString('N0'))" -ForegroundColor White
	Write-Host "  Pending: $($status.pendingSegments.ToString('N0'))" -ForegroundColor White
	Write-Host "  Assigned: $($status.assignedSegments.ToString('N0'))" -ForegroundColor White
	Write-Host ""
} catch {
	Write-Host "  (Could not fetch status)" -ForegroundColor Gray
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "✅ Ready for workers!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
