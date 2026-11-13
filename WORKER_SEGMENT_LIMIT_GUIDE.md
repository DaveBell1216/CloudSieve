# Worker Segment Limit Configuration

## Updated Behavior

The worker has been modified to **process exactly 25 segments and then automatically terminate**.

## Changes Made

### 1. Added Segment Counter
```csharp
int maxSegments = 25; // Process 25 segments then terminate
int segmentsProcessed = 0;
```

### 2. Modified Loop Condition
- **Before:** `while (true)` - Ran indefinitely
- **After:** `while (segmentsProcessed < maxSegments)` - Stops after 25 segments

### 3. Progress Tracking
- Shows segment count with each segment: `segment 1/25`, `segment 2/25`, etc.
- Increments counter only after successful submission
- Shows completion message when done

## Running the Modified Worker

### Start the Worker
```powershell
cd Seive2.Worker
dotnet run -- http://192.168.1.9:5150
```

### What You'll See

**At Startup:**
```
=========================================
  Distributed Prime Sieve - Worker Node
=========================================
Worker ID: DEVELOPMENT
Worker Name: DEVELOPMENT-Worker
Server URL: http://192.168.1.9:5150
Max Segments: 25
=========================================
```

**During Processing:**
```
[16:45:23] Received segment 1/25: 2 - 100,000,001
[16:45:23] Completed in 0.15s - Found 5,761,455 primes
[16:45:23] Result submitted successfully (1/25 completed)

[16:45:23] Received segment 2/25: 100,000,002 - 200,000,001
[16:45:23] Completed in 0.14s - Found 5,428,681 primes
[16:45:23] Result submitted successfully (2/25 completed)

...

[16:45:30] Received segment 25/25: 2,400,000,002 - 2,500,000,001
[16:45:30] Completed in 0.14s - Found 4,981,234 primes
[16:45:30] Result submitted successfully (25/25 completed)
```

**At Completion:**
```
=========================================
✅ Worker completed 25 segments
Worker terminating...
=========================================
```

## Why This is Useful

### 1. **Controlled Processing**
- Each worker processes a fixed amount of work
- Prevents any single worker from running indefinitely

### 2. **Easy to Scale**
- Start multiple workers to parallelize the job
- Each processes 25 segments then exits
- Restart as needed for more work

### 3. **Resource Management**
- Prevents memory leaks from long-running processes
- Allows for clean restarts
- Good for containerized/cloud deployments

### 4. **Progress Tracking**
- Clear visibility of each worker's progress
- Know exactly how much work each worker does

## For Your 100 Billion Job

**Current Job Details:**
- Total Segments: 1,000
- Segments per Worker: 25
- **Workers Needed:** 1,000 ÷ 25 = **40 workers** to complete the job

### Running Multiple Workers

**Option 1: Sequential** (slower)
```powershell
# Run one worker at a time, 40 times total
dotnet run -- http://192.168.1.9:5150
# Wait for it to complete 25 segments and exit
# Then run again...
```

**Option 2: Parallel** (much faster!)
```powershell
# Terminal 1
dotnet run -- http://192.168.1.9:5150

# Terminal 2
dotnet run -- http://192.168.1.9:5150

# Terminal 3
dotnet run -- http://192.168.1.9:5150

# ... start as many as you want!
```

With 8 parallel workers, you'd only need to restart them 5 times (8 × 5 = 40 runs × 25 segments = 1,000 segments).

## Changing the Segment Limit

If you want to change from 25 segments to a different number, edit `Program.cs`:

```csharp
int maxSegments = 50; // Change this value
```

Popular configurations:
- **10 segments** - Quick test, multiple restarts needed
- **25 segments** - Current setting (balanced)
- **50 segments** - Fewer restarts
- **100 segments** - Long-running workers

## Monitoring Overall Job Progress

While workers are running, check the job status:

```powershell
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/status" | ConvertTo-Json
```

Or use the browser interface (`PrimeSieveController.html`) and click "📊 Check Status".

## Example Session

```powershell
# Start 4 workers in parallel (4 separate PowerShell windows)

# Terminal 1
PS> cd Seive2.Worker
PS> dotnet run -- http://192.168.1.9:5150
# Processes segments 1-25, then exits

# Terminal 2
PS> dotnet run -- http://192.168.1.9:5150
# Processes segments 26-50, then exits

# Terminal 3
PS> dotnet run -- http://192.168.1.9:5150
# Processes segments 51-75, then exits

# Terminal 4
PS> dotnet run -- http://192.168.1.9:5150
# Processes segments 76-100, then exits

# After all 4 complete, restart them...
# Repeat 10 times total to process all 1,000 segments
```

## Benefits for Your 100 Billion Prime Job

With **1,000 segments** total and **25 segments per worker**:

| Workers | Runs Needed | Total Worker Instances |
|---------|-------------|----------------------|
| 1 worker | 40 runs | 40 instances |
| 2 workers | 20 runs | 40 instances |
| 4 workers | 10 runs | 40 instances |
| 8 workers | 5 runs | 40 instances |
| 10 workers | 4 runs | 40 instances |

**Recommendation:** Start 8-10 workers in parallel for optimal performance.

## Automatic Restart Script

Want to automatically restart workers? Create `RunWorkers.ps1`:

```powershell
# RunWorkers.ps1
$runs = 5  # Number of times to restart
for ($i = 1; $i -le $runs; $i++) {
	Write-Host "`n=== RUN $i of $runs ===" -ForegroundColor Cyan
	dotnet run --project Seive2.Worker -- http://192.168.1.9:5150
	Write-Host "Run $i completed" -ForegroundColor Green
}
Write-Host "`n✅ All runs completed!" -ForegroundColor Green
```

Run it:
```powershell
.\RunWorkers.ps1
```

This will automatically process 5 × 25 = 125 segments!
