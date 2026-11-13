# Deploy API to Server 192.168.1.9

## Files Ready for Deployment

The API has been published to:
```
C:\Users\administrators1\source\repos\Seive2\publish\api\
```

## Deployment Steps

### Step 1: Copy Files to Server

**Option A: Network Share**
If you have a network share to 192.168.1.9:
```powershell
# Example: Copy to server's shared folder
Copy-Item -Path "C:\Users\administrators1\source\repos\Seive2\publish\api\*" -Destination "\\192.168.1.9\c$\inetpub\Seive2.Api\" -Recurse -Force
```

**Option B: Remote Desktop**
1. Remote Desktop to 192.168.1.9
2. Copy the `publish\api` folder to the server (via RDP clipboard or network)
3. Place in a suitable location, e.g., `C:\Apps\Seive2.Api\`

**Option C: SCP/SFTP** (if Linux server)
```powershell
scp -r publish/api/* user@192.168.1.9:/opt/Seive2.Api/
```

### Step 2: Run API on Server

**Connect to the server** and run:

```powershell
# Navigate to deployment folder
cd C:\Apps\Seive2.Api  # (or wherever you copied it)

# Run the API
dotnet Seive2.Api.dll --urls "http://192.168.1.9:5150"
```

**Or to run on all interfaces:**
```powershell
dotnet Seive2.Api.dll --urls "http://0.0.0.0:5150"
```

### Step 3: Start the Job (from Development Machine)

Once the API is running on the server, start the job from your development machine:

```powershell
$body = @{
	maxNumber = 100000000000
	segmentSize = 100000
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" -Method Post -Body $body -ContentType "application/json" | ConvertTo-Json
```

### Step 4: Verify Job Created

```powershell
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/status" -Method Get | ConvertTo-Json
```

---

## Alternative: Remote PowerShell Execution

If you have PowerShell remoting enabled on the server:

### Step 1: Enable Remote PowerShell (One-Time Setup on Server)
On the server (192.168.1.9), run as Administrator:
```powershell
Enable-PSRemoting -Force
Set-Item WSMan:\localhost\Client\TrustedHosts -Value "192.168.1.9" -Concatenate -Force
```

On development machine:
```powershell
Set-Item WSMan:\localhost\Client\TrustedHosts -Value "192.168.1.9" -Force
```

### Step 2: Deploy and Start API Remotely

```powershell
# Copy files to server
$session = New-PSSession -ComputerName 192.168.1.9 -Credential (Get-Credential)

Copy-Item -Path "C:\Users\administrators1\source\repos\Seive2\publish\api" -Destination "C:\Apps\" -ToSession $session -Recurse -Force

# Start API on server
Invoke-Command -Session $session -ScriptBlock {
	cd C:\Apps\api
	Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet Seive2.Api.dll --urls 'http://0.0.0.0:5150'"
}

Remove-PSSession $session
```

### Step 3: Start the Job
```powershell
$body = @{ maxNumber = 100000000000; segmentSize = 100000 } | ConvertTo-Json
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" -Method Post -Body $body -ContentType "application/json"
```

---

## Option: Run as Windows Service (Production)

For a permanent deployment, install the API as a Windows Service on the server.

### Create Service Wrapper Script
On the server, create `C:\Apps\Seive2.Api\run-api.ps1`:
```powershell
Set-Location "C:\Apps\Seive2.Api"
dotnet Seive2.Api.dll --urls "http://0.0.0.0:5150"
```

### Install as Service (using NSSM or sc.exe)

**Option A: Using NSSM (Non-Sucking Service Manager)**
1. Download NSSM from https://nssm.cc/download
2. On server:
```powershell
nssm install Seive2Api "C:\Program Files\dotnet\dotnet.exe" "C:\Apps\Seive2.Api\Seive2.Api.dll --urls http://0.0.0.0:5150"
nssm set Seive2Api AppDirectory "C:\Apps\Seive2.Api"
nssm start Seive2Api
```

**Option B: Using sc.exe**
```powershell
sc.exe create Seive2Api binPath= "C:\Program Files\dotnet\dotnet.exe C:\Apps\Seive2.Api\Seive2.Api.dll --urls http://0.0.0.0:5150" start= auto
sc.exe start Seive2Api
```

---

## Firewall Configuration

If the API is not accessible, open the firewall port on the server:

```powershell
# On server (as Administrator)
New-NetFirewallRule -DisplayName "Seive2 API" -Direction Inbound -Protocol TCP -LocalPort 5150 -Action Allow
```

---

## Quick Test After Deployment

From your development machine:

### Test 1: Check Health
```powershell
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/health"
```
Expected: `{ "status": "healthy" }`

### Test 2: Start Job
```powershell
$body = @{ maxNumber = 100000000000; segmentSize = 100000 } | ConvertTo-Json
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" -Method Post -Body $body -ContentType "application/json"
```

### Test 3: Check Status
```powershell
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/status"
```

---

## Worker Configuration

Workers can run from **anywhere** (development machine, server, other machines):

```powershell
# On development machine
cd C:\Users\administrators1\source\repos\Seive2\Seive2.Worker
dotnet run -- http://192.168.1.9:5150

# Or publish and run on other machines
dotnet publish Seive2.Worker/Seive2.Worker.csproj -c Release -o publish/worker
# Copy publish/worker to any machine and run:
# dotnet Seive2.Worker.dll http://192.168.1.9:5150
```

---

## Current Status

✅ **API Published:** `C:\Users\administrators1\source\repos\Seive2\publish\api\`  
⏳ **Next Step:** Copy to server 192.168.1.9 and start

**Choose your deployment method above and run the API on the server!**
