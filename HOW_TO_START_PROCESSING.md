# How to Start Processing Numbers - Quick Guide

## Your API Configuration
- **API Address**: `http://192.168.1.9:5150`

## Step-by-Step Process

### 1. Start a Job (Create Work)
Use the API to create a new prime sieve job:

**Using PowerShell:**
```powershell
$body = @{ 
	maxNumber = 10000000      # Find primes up to 10 million
	segmentSize = 1000000     # Process in 1 million chunks
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" `
	-Method POST -Body $body -ContentType "application/json"
```

**Using the Seive2.http file:**
Click "Send Request" on the "Start a new sieve job" section.

### 2. Start Worker(s) to Process the Job
Workers do the actual number crunching:

**Start a worker:**
```powershell
cd Seive2.Worker
dotnet run -- http://192.168.1.9:5150
```

Or if you're already in the worker directory:
```powershell
dotnet run -- http://192.168.1.9:5150
```

**Start multiple workers for faster processing:**
```powershell
# Terminal 1
dotnet run -- http://192.168.1.9:5150

# Terminal 2
dotnet run -- http://192.168.1.9:5150

# Terminal 3
dotnet run -- http://192.168.1.9:5150
```

### 3. Monitor Progress

**Check job status:**
```powershell
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/status" -Method GET
```

Or use the Seive2.http file "Get current job status" request.

## Architecture Overview

```
┌─────────────────┐
│   API Server    │ ← Creates jobs, coordinates work
│  192.168.1.9    │
│     :5150       │
└────────┬────────┘
		 │
	┌────┴────┬────────┬────────┐
	│         │        │        │
┌───▼───┐ ┌──▼───┐ ┌──▼───┐ ┌──▼───┐
│Worker1│ │Worker2│ │Worker3│ │Worker4│ ← Process numbers
└───────┘ └──────┘ └──────┘ └──────┘
```

## What Just Happened (Your Recent Run)

✅ **Job Completed Successfully!**
- **Max Number**: 10,000,000
- **Primes Found**: 664,579
- **Time**: 12.9 seconds
- **Segments**: 10 (each 1 million numbers)

## Example: Find Primes up to 1 Billion

```powershell
# 1. Start the job
$body = @{ 
	maxNumber = 1000000000     # 1 billion
	segmentSize = 10000000     # 10 million per segment
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" `
	-Method POST -Body $body -ContentType "application/json"

# 2. Start workers (in separate terminals for parallel processing)
dotnet run --project Seive2.Worker -- http://192.168.1.9:5150
```

## Worker Output Example

```
=========================================
  Distributed Prime Sieve - Worker Node
=========================================
Worker ID: DEVELOPMENT
Worker Name: DEVELOPMENT-Worker
Server URL: http://192.168.1.9:5150
=========================================
[16:09:30] Received segment: 2 - 1,000,001
[16:09:30] Completed in 0.01s - Found 78,498 primes
[16:09:30] Result submitted successfully
[16:09:30] Received segment: 1,000,002 - 2,000,001
[16:09:30] Completed in 0.01s - Found 70,435 primes
...
```

## Troubleshooting

### Worker says "No work available"
- ✅ Make sure you've started a job first (Step 1)
- ✅ Check that you're connecting to the correct API address

### Worker says "Connection refused"
- ✅ Verify API is running on http://192.168.1.9:5150
- ✅ Check firewall/network settings
- ✅ Make sure you're passing the correct server URL to the worker

### Check API is running
```powershell
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/health" -Method GET
```

Should return: `{ "status": "healthy", "timestamp": "..." }`

## Performance Tips

1. **More Workers = Faster Processing**
   - Start 4-8 workers for optimal performance
   - Each worker processes segments in parallel

2. **Segment Size Matters**
   - Smaller segments (100K-1M): Better load balancing
   - Larger segments (10M-100M): Less overhead
   - Default 1M is good for most cases

3. **Network Workers**
   - You can run workers on different machines
   - Just point them to http://192.168.1.9:5150
   - Great for distributing the workload

## Quick Test

Want to test? Run this:

```powershell
# Start a small job
$body = @{ maxNumber = 100000; segmentSize = 10000 } | ConvertTo-Json
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" -Method POST -Body $body -ContentType "application/json"

# Start a worker
cd Seive2.Worker
dotnet run -- http://192.168.1.9:5150
```

Should complete in under a second!
