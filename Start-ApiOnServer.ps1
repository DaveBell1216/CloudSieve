# Quick Deployment Script
# Run this FROM THE SERVER (192.168.1.9) after copying the files

param(
	[string]$ApiPath = "C:\Apps\Seive2.Api",
	[switch]$StartJob,
	[long]$MaxNumber = 100000000000,
	[long]$SegmentSize = 100000
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Seive2 API - Server Deployment" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Check if files exist
if (-not (Test-Path "$ApiPath\Seive2.Api.dll")) {
	Write-Host "ERROR: API files not found at $ApiPath" -ForegroundColor Red
	Write-Host ""
	Write-Host "Please copy the published files from:" -ForegroundColor Yellow
	Write-Host "  Source: C:\Users\administrators1\source\repos\Seive2\publish\api\" -ForegroundColor Yellow
	Write-Host "  Destination: $ApiPath" -ForegroundColor Yellow
	Write-Host ""
	Write-Host "You can copy them from your development machine:" -ForegroundColor Cyan
	Write-Host "  1. Via network share:" -ForegroundColor Gray
	Write-Host "     Copy-Item 'C:\Users\administrators1\source\repos\Seive2\publish\api\*' '\\192.168.1.9\c$\Apps\Seive2.Api\' -Recurse" -ForegroundColor Gray
	Write-Host "  2. Via Remote Desktop (copy/paste)" -ForegroundColor Gray
	Write-Host "  3. Via USB drive or network location" -ForegroundColor Gray
	exit 1
}

Write-Host "✅ API files found at $ApiPath" -ForegroundColor Green

# Check if port 5150 is already in use
$portInUse = Get-NetTCPConnection -LocalPort 5150 -ErrorAction SilentlyContinue
if ($portInUse) {
	Write-Host ""
	Write-Host "⚠️  Port 5150 is already in use!" -ForegroundColor Yellow
	Write-Host "   Attempting to stop existing process..." -ForegroundColor Yellow

	$proc = Get-Process -Id $portInUse[0].OwningProcess -ErrorAction SilentlyContinue
	if ($proc) {
		Write-Host "   Stopping process: $($proc.ProcessName) (PID $($proc.Id))" -ForegroundColor Gray
		Stop-Process -Id $proc.Id -Force
		Start-Sleep 2
	}
}

# Check firewall
Write-Host ""
Write-Host "Checking firewall..." -ForegroundColor Cyan
$firewallRule = Get-NetFirewallRule -DisplayName "Seive2 API" -ErrorAction SilentlyContinue
if (-not $firewallRule) {
	Write-Host "⚠️  Firewall rule not found. Creating..." -ForegroundColor Yellow
	try {
		New-NetFirewallRule -DisplayName "Seive2 API" -Direction Inbound -Protocol TCP -LocalPort 5150 -Action Allow | Out-Null
		Write-Host "✅ Firewall rule created" -ForegroundColor Green
	} catch {
		Write-Host "❌ Failed to create firewall rule (run as Administrator)" -ForegroundColor Red
	}
} else {
	Write-Host "✅ Firewall rule exists" -ForegroundColor Green
}

# Start API
Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Starting API on http://0.0.0.0:5150..." -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "API will be accessible at:" -ForegroundColor Yellow
Write-Host "  - http://192.168.1.9:5150 (from network)" -ForegroundColor White
Write-Host "  - http://localhost:5150 (from this server)" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C to stop the API" -ForegroundColor Gray
Write-Host ""

Set-Location $ApiPath

# Start API (this will block until Ctrl+C)
if ($StartJob) {
	# Start API in background and then create job
	$apiJob = Start-Job -ScriptBlock {
		param($path)
		Set-Location $path
		dotnet Seive2.Api.dll --urls "http://0.0.0.0:5150"
	} -ArgumentList $ApiPath

	Write-Host "Waiting for API to start..." -ForegroundColor Cyan
	Start-Sleep 5

	Write-Host ""
	Write-Host "Creating job..." -ForegroundColor Cyan
	Write-Host "  Max Number: $($MaxNumber.ToString('N0'))" -ForegroundColor White
	Write-Host "  Segment Size: $($SegmentSize.ToString('N0'))" -ForegroundColor White

	try {
		$body = @{
			maxNumber = $MaxNumber
			segmentSize = $SegmentSize
		} | ConvertTo-Json

		$result = Invoke-RestMethod -Uri "http://localhost:5150/api/job/start" -Method Post -Body $body -ContentType "application/json"

		Write-Host ""
		Write-Host "✅ Job Created!" -ForegroundColor Green
		Write-Host "   Job ID: $($result.jobId)" -ForegroundColor White
		Write-Host "   Total Segments: $($result.totalSegments.ToString('N0'))" -ForegroundColor White
		Write-Host ""
	} catch {
		Write-Host ""
		Write-Host "❌ Failed to create job: $_" -ForegroundColor Red
		Stop-Job $apiJob
		Remove-Job $apiJob
		exit 1
	}

	Write-Host "API is now running (Job ID: $($apiJob.Id))" -ForegroundColor Cyan
	Write-Host "To view API output: Receive-Job -Id $($apiJob.Id) -Keep" -ForegroundColor Gray
	Write-Host "To stop API: Stop-Job -Id $($apiJob.Id); Remove-Job -Id $($apiJob.Id)" -ForegroundColor Gray
	Write-Host ""

} else {
	# Run API in foreground
	dotnet Seive2.Api.dll --urls "http://0.0.0.0:5150"
}
