# API Restarted - Job Configuration

## ✅ API Status

**URL:** `http://localhost:5150`  
**Status:** Running  
**Started:** 2026-07-31 00:34:25 UTC

---

## 📊 Current Job Details

### Job Parameters
- **Job ID:** `02eb373c-bcc1-437d-acc2-baced16150ec`
- **Max Number:** `100,000,000,000` (100 billion)
- **Segment Size:** `100,000`
- **Total Segments:** `1,000,000` (1 million segments!)

### Job Status
- **Status:** In Progress (Waiting for workers)
- **Completed Segments:** 0
- **Pending Segments:** 1,000,000
- **Assigned Segments:** 0
- **Progress:** 0.0%

### Job Scope
- **Range:** 2 to 100,000,000,000
- **Each Segment:** 100,000 numbers
- **Segments per Worker (25 limit):** Each worker will process only 25 of the 1 million segments

---

## 🔢 The Math

### Total Work
- **Total numbers to check:** ~100 billion
- **Divided into segments:** 1 million segments of 100k numbers each
- **Estimated primes:** ~2.8 billion primes (based on prime number theorem)

### Worker Capacity
With the current worker configuration:
- **Each worker processes:** 25 segments
- **Numbers per worker:** 25 × 100,000 = 2,500,000 numbers
- **Workers needed to complete:** 1,000,000 ÷ 25 = 40,000 workers!

### Time Estimates
If a worker completes 25 segments in ~30 seconds:
- **Per segment:** ~1.2 seconds
- **Total time** (if running workers continuously): ~333 hours (~14 days)
- **With 100 parallel workers:** ~3.3 hours
- **With 1000 parallel workers:** ~20 minutes

---

## 🚀 How to Start Workers

### Start a Single Worker (25 segments)
```powershell
cd Seive2.Worker
dotnet run -- http://localhost:5150
```

The worker will:
1. Process exactly 25 segments
2. Print progress: `(1/25 completed)`, `(2/25 completed)`, etc.
3. Exit automatically after `(25/25 completed)`

### Start Multiple Workers (Parallel Processing)

**Option 1: Manual - Multiple Windows**
Open multiple PowerShell windows and run the worker command in each.

**Option 2: Scripted - Background Jobs**
```powershell
# Start 10 workers
1..10 | ForEach-Object {
	Start-Job -ScriptBlock {
		cd C:\Users\administrators1\source\repos\Seive2\Seive2.Worker
		dotnet run -- http://localhost:5150
	} -Name "Worker$_"
}

# Check status
Get-Job

# View output from a specific worker
Receive-Job -Name Worker1 -Keep

# Stop all workers
Get-Job | Stop-Job
```

**Option 3: Scripted - Separate Processes**
```powershell
# Start 10 workers in separate PowerShell windows
1..10 | ForEach-Object {
	Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd C:\Users\administrators1\source\repos\Seive2\Seive2.Worker; dotnet run -- http://localhost:5150"
}
```

---

##  Monitoring Progress

### Check Job Status via REST API
```powershell
Invoke-RestMethod -Uri "http://localhost:5150/api/job/status" -Method Get | ConvertTo-Json
```

### Watch Progress in Real-Time
```powershell
while ($true) {
	$status = Invoke-RestMethod -Uri "http://localhost:5150/api/job/status"
	Clear-Host
	Write-Host "========================================" -ForegroundColor Cyan
	Write-Host "  Prime Sieve Job Status" -ForegroundColor Cyan
	Write-Host "========================================" -ForegroundColor Cyan
	Write-Host "Total Segments: $($status.totalSegments.ToString('N0'))" -ForegroundColor White
	Write-Host "Completed: $($status.completedSegments.ToString('N0'))" -ForegroundColor Green
	Write-Host "Pending: $($status.pendingSegments.ToString('N0'))" -ForegroundColor Yellow
	Write-Host "Assigned: $($status.assignedSegments.ToString('N0'))" -ForegroundColor Cyan
	Write-Host "Progress: $($status.progressPercentage.ToString('F2'))%" -ForegroundColor Magenta
	Write-Host "Primes Found: $($status.totalPrimesFound.ToString('N0'))" -ForegroundColor Green
	Write-Host "Elapsed: $($status.elapsedTime)" -ForegroundColor Gray
	Write-Host "========================================" -ForegroundColor Cyan
	Start-Sleep -Seconds 5
}
```

### Open in Browser
The HTML controller should work at:
```
file:///C:/Users/administrators1/source/repos/Seive2/PrimeSieveController.html
```
Update the API URL in the HTML to `http://localhost:5150` if needed.

---

## ⚠️ Important Notes

### Why 1 Million Segments?
With a segment size of 100,000 and max number of 100 billion:
- Total segments = 100,000,000,000 ÷ 100,000 = 1,000,000

### Worker Termination
Each worker is configured to process **exactly 25 segments** and then terminate. This has been verified and is working correctly.

### Segment Size vs Total Segments
- **Smaller segments** = More total segments = More coordination overhead
- **Larger segments** = Fewer segments = Longer processing time per segment
- **100k segment size** is a reasonable balance

### Memory Implications
- API memory: ~100 MB for tracking 1 million segments
- Worker memory per segment: ~13 MB (BitArray for 100k numbers)
- Total worker memory: ~325 MB (for 25 segments in queue)

---

## 🛑 How to Stop Everything

### Stop the API
Press `Ctrl+C` in the API terminal, or:
```powershell
Get-Process | Where-Object {$_.ProcessName -eq "dotnet"} | Stop-Process
```

### Stop All Workers
If using background jobs:
```powershell
Get-Job | Stop-Job
Get-Job | Remove-Job
```

If using separate processes:
```powershell
Get-Process | Where-Object {$_.MainWindowTitle -like "*dotnet run*"} | Stop-Process
```

---

## 📝 Summary

✅ **API Running:** http://localhost:5150  
✅ **Job Created:** 100 billion max, 100k segments  
✅ **Total Segments:** 1,000,000  
✅ **Workers Ready:** Start with command above  
✅ **Worker Quota:** 25 segments per worker (verified working)  

**The system is ready for prime number processing!**

To make meaningful progress, you'll want to start multiple workers in parallel. Each will grab 25 segments and exit, so you can run them in a loop or batch.
