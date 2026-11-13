# Deployment Summary - Server vs Development Machine

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                                                               │
│  DEVELOPMENT MACHINE (localhost)                            │
│  - Build and compile code                                   │
│  - Run workers (optional)                                   │
│  - Send job requests                                        │
│  - Monitor progress                                         │
│                                                               │
└────────────────┬────────────────────────────────────────────┘
				 │
				 │ HTTP Requests
				 ↓
┌─────────────────────────────────────────────────────────────┐
│                                                               │
│  SERVER (192.168.1.9:5150)                                  │
│  - Run API (job coordinator)                                │
│  - Manage job state                                         │
│  - Distribute work segments                                 │
│  - Collect results                                          │
│  - Run workers (optional)                                   │
│                                                               │
└─────────────────────────────────────────────────────────────┘
				 ↑
				 │ Work requests
				 │
┌────────────────┴────────────────────────────────────────────┐
│                                                               │
│  WORKERS (can run anywhere)                                 │
│  - Request work segments from API                           │
│  - Calculate primes for assigned range                      │
│  - Submit results back to API                               │
│  - Process exactly 25 segments and exit                     │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

---

## Current Status

### ✅ What's Done

- [x] API code compiled and published
- [x] Published files ready at: `C:\Users\administrators1\source\repos\Seive2\publish\api\`
- [x] Worker code tested and verified (terminates after 25 segments)
- [x] Deployment scripts created

### ⏳ What's Needed

- [ ] Copy API files to server (192.168.1.9)
- [ ] Start API on server
- [ ] Create job with specified parameters (100B max, 100k segments)
- [ ] Start workers

---

## Step-by-Step Instructions

### STEP 1: Deploy API to Server (192.168.1.9)

**On your development machine**, the API files are ready at:
```
C:\Users\administrators1\source\repos\Seive2\publish\api\
```

**Copy these files to the server.** Choose one method:

#### Method A: Network Share (Easiest)
From development machine:
```powershell
# Adjust destination path as needed
Copy-Item -Path "C:\Users\administrators1\source\repos\Seive2\publish\api\*" `
		  -Destination "\\192.168.1.9\c$\Apps\Seive2.Api\" `
		  -Recurse -Force
```

#### Method B: Remote Desktop
1. Connect via RDP to 192.168.1.9
2. Copy the `publish\api` folder (via RDP clipboard or shared drive)
3. Paste to `C:\Apps\Seive2.Api\` on the server

#### Method C: PowerShell Remoting
```powershell
$session = New-PSSession -ComputerName 192.168.1.9 -Credential (Get-Credential)
Copy-Item -Path "C:\Users\administrators1\source\repos\Seive2\publish\api" `
		  -Destination "C:\Apps\" `
		  -ToSession $session -Recurse
Remove-PSSession $session
```

---

### STEP 2: Start API on Server

**On the server (192.168.1.9)**, run the deployment script:

```powershell
# Copy the script to server first
# Then run it:
cd C:\Apps\Seive2.Api
.\Start-ApiOnServer.ps1
```

**Or manually:**
```powershell
cd C:\Apps\Seive2.Api
dotnet Seive2.Api.dll --urls "http://0.0.0.0:5150"
```

You should see:
```
info: Microsoft.Hosting.Lifetime[14]
	  Now listening on: http://0.0.0.0:5150
info: Microsoft.Hosting.Lifetime[0]
	  Application started. Press Ctrl+C to shut down.
```

---

### STEP 3: Create Job (FROM DEVELOPMENT MACHINE)

**On your development machine**, run:

```powershell
.\Start-JobOnServer.ps1
```

This will:
- Test connection to `http://192.168.1.9:5150`
- Start job with MaxNumber=100,000,000,000 and SegmentSize=100,000
- Display job details

**Or manually:**
```powershell
$body = @{
	maxNumber = 100000000000
	segmentSize = 100000
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" `
				  -Method Post `
				  -Body $body `
				  -ContentType "application/json"
```

Expected response:
```json
{
  "jobId": "...",
  "maxNumber": 100000000000,
  "segmentSize": 100000,
  "totalSegments": 1000000,
  "startedAt": "...",
  "status": 1
}
```

---

### STEP 4: Start Workers

Workers can run from **anywhere** that can reach `http://192.168.1.9:5150`.

#### From Development Machine:
```powershell
cd C:\Users\administrators1\source\repos\Seive2\Seive2.Worker
dotnet run -- http://192.168.1.9:5150
```

#### From Server (192.168.1.9):
First, publish the worker:
```powershell
# On development machine
dotnet publish Seive2.Worker/Seive2.Worker.csproj -c Release -o publish/worker

# Copy to server
Copy-Item -Path "publish\worker\*" -Destination "\\192.168.1.9\c$\Apps\Seive2.Worker\" -Recurse

# On server
cd C:\Apps\Seive2.Worker
dotnet Seive2.Worker.dll http://192.168.1.9:5150
```

#### Run Multiple Workers in Parallel:
```powershell
# Start 10 workers
1..10 | ForEach-Object {
	Start-Process powershell -ArgumentList "-NoExit", "-Command", `
		"cd C:\Users\administrators1\source\repos\Seive2\Seive2.Worker; dotnet run -- http://192.168.1.9:5150"
}
```

Each worker will:
1. Request work from API
2. Process exactly 25 segments
3. Submit results
4. Exit automatically

---

### STEP 5: Monitor Progress

#### Check Status (Quick)
```powershell
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/status" | ConvertTo-Json
```

#### Watch Progress in Real-Time
```powershell
while ($true) {
	$status = Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/status"
	Clear-Host
	Write-Host "Segments: $($status.completedSegments)/$($status.totalSegments) ($($status.progressPercentage.ToString('F2'))%)"
	Write-Host "Primes Found: $($status.totalPrimesFound.ToString('N0'))"
	Write-Host "Pending: $($status.pendingSegments) | Assigned: $($status.assignedSegments)"
	Start-Sleep 5
}
```

---

## Files Created for Deployment

| File | Location | Purpose |
|------|----------|---------|
| **API (published)** | `publish\api\` | Ready-to-deploy API files |
| **Worker (source)** | `Seive2.Worker\` | Worker source (can publish if needed) |
| **Start-ApiOnServer.ps1** | Root | Run ON SERVER to start API |
| **Start-JobOnServer.ps1** | Root | Run FROM DEV MACHINE to create job |
| **DEPLOY_API_TO_SERVER.md** | Root | Detailed deployment guide |

---

## Quick Reference Commands

### On Server (192.168.1.9)
```powershell
# Start API
cd C:\Apps\Seive2.Api
dotnet Seive2.Api.dll --urls "http://0.0.0.0:5150"
```

### On Development Machine
```powershell
# Create job
.\Start-JobOnServer.ps1

# Or manually
$body = @{ maxNumber = 100000000000; segmentSize = 100000 } | ConvertTo-Json
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" -Method Post -Body $body -ContentType "application/json"

# Check status
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/status" | ConvertTo-Json

# Start a worker
cd Seive2.Worker
dotnet run -- http://192.168.1.9:5150
```

---

## Troubleshooting

### "Cannot connect to API"
1. Ensure API is running on server
2. Check firewall: `New-NetFirewallRule -DisplayName "Seive2 API" -Direction Inbound -Protocol TCP -LocalPort 5150 -Action Allow`
3. Test from server: `Invoke-RestMethod http://localhost:5150/api/job/health`
4. Test from dev machine: `Test-NetConnection -ComputerName 192.168.1.9 -Port 5150`

### "Port already in use"
```powershell
# Find and kill process using port 5150
$conn = Get-NetTCPConnection -LocalPort 5150
Stop-Process -Id $conn.OwningProcess -Force
```

### "Worker not getting work"
1. Check job exists: `Invoke-RestMethod http://192.168.1.9:5150/api/job/status`
2. Verify worker URL: Should be `http://192.168.1.9:5150` (not localhost)
3. Check API is reachable from worker machine

---

## Summary

✅ **API Published:** `publish\api\`  
📋 **Next Steps:**  
1. Copy API files to server (192.168.1.9)  
2. Run `Start-ApiOnServer.ps1` on server  
3. Run `Start-JobOnServer.ps1` from dev machine  
4. Start workers  

**All files and scripts are ready. Follow Steps 1-4 above to deploy!**
