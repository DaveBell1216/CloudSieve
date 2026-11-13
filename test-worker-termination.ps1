# Test script to diagnose worker termination issue
# This will run the worker and capture its output

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Worker Termination Test" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Starting worker with debug logging..." -ForegroundColor Yellow
Write-Host "The worker should process exactly 25 segments and exit" -ForegroundColor Yellow
Write-Host ""
Write-Host "Watch for these key messages:" -ForegroundColor Green
Write-Host "  - 'Loop start' at the beginning of each iteration" -ForegroundColor Gray
Write-Host "  - 'X/25 completed' after each successful submission" -ForegroundColor Gray
Write-Host "  - '✅ Segment limit reached' when quota is met" -ForegroundColor Gray
Write-Host "  - 'Breaking from loop' just before exit" -ForegroundColor Gray
Write-Host "  - 'Exited main loop' after the loop completes" -ForegroundColor Gray
Write-Host ""
Write-Host "Press Ctrl+C to stop manually if it doesn't exit" -ForegroundColor Red
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Change to worker directory
Set-Location "Seive2.Worker"

# Run the worker
dotnet run -- http://192.168.1.9:5150

# This line will only execute if the worker exits cleanly
Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "✅ Worker exited cleanly!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
