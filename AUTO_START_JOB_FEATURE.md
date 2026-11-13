# Auto-Start Job Feature ✅

## What Changed

The API has been modified to **automatically start a job** when the application starts (including after app pool recycling).

---

## Auto-Start Configuration

### Default Settings
When the API starts, it automatically creates a job with:

- **Max Number:** `4,294,967,295` (uint.MaxValue, ~4.3 billion)
- **Segment Size:** `100,000`
- **Total Segments:** `42,950`

### What This Means

✅ **App Pool Recycle:** Job auto-starts  
✅ **Server Restart:** Job auto-starts  
✅ **dotnet run:** Job auto-starts  
✅ **Windows Service Start:** Job auto-starts  

**You never need to manually start a job anymore!**

---

## Startup Logs

When the API starts, you'll see these log messages:

```
info: Seive2.HostedServices.AutoStartJobService[0]
	  Auto-starting sieve job on application startup
info: Seive2.HostedServices.AutoStartJobService[0]
	  Max Number: 4,294,967,295
info: Seive2.HostedServices.AutoStartJobService[0]
	  Segment Size: 100,000
info: Seive2.HostedServices.AutoStartJobService[0]
	  ✅ Job auto-started successfully!
info: Seive2.HostedServices.AutoStartJobService[0]
	  Job ID: 1bb3931b-19fe-4b8f-ac61-e11682bfdb67
info: Seive2.HostedServices.AutoStartJobService[0]
	  Total Segments: 42,950
info: Seive2.HostedServices.AutoStartJobService[0]
	  Ready for workers to connect and start processing
info: Microsoft.Hosting.Lifetime[14]
	  Now listening on: http://localhost:5150
```

---

## Verify Auto-Start Worked

After the API starts, check the job status:

```powershell
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/status"
```

You should immediately see:
```json
{
  "jobId": "...",
  "status": 1,
  "maxNumber": 4294967295,
  "totalSegments": 42950,
  "pendingSegments": 42950,
  "completedSegments": 0
}
```

---

## How It Works

### New File: `AutoStartJobService.cs`
A background hosted service that runs when the application starts:

```csharp
public class AutoStartJobService : IHostedService
{
	public Task StartAsync(CancellationToken cancellationToken)
	{
		const long maxNumber = uint.MaxValue; // 4,294,967,295
		const long segmentSize = 100_000;

		var job = _coordinator.StartJob(maxNumber, segmentSize);

		_logger.LogInformation("✅ Job auto-started successfully!");
		return Task.CompletedTask;
	}
}
```

### Modified: `Program.cs`
Registered the auto-start service:

```csharp
// Register auto-start job service
builder.Services.AddHostedService<AutoStartJobService>();
```

---

## Customizing Auto-Start Settings

If you want to change the default max number or segment size, edit:

**File:** `Seive2/HostedServices/AutoStartJobService.cs`

**Change these lines:**
```csharp
const long maxNumber = uint.MaxValue; // Change this
const long segmentSize = 100_000;     // Change this
```

**Example - Change to 100 billion:**
```csharp
const long maxNumber = 100_000_000_000; // 100 billion
const long segmentSize = 100_000;
```

Then rebuild and republish:
```powershell
dotnet publish Seive2/Seive2.Api.csproj -c Release -o publish/api
```

---

## Manual Job Start Still Works

You can still manually start a job with different parameters:

```powershell
$body = @{ maxNumber = 100000000000; segmentSize = 50000 } | ConvertTo-Json
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" `
				  -Method Post -Body $body -ContentType "application/json"
```

**However:** This will fail if the auto-started job is still running.

To allow manual restart, you'd need to stop the current job first (not currently implemented).

---

## IIS App Pool Recycling

When running in IIS, the API will auto-start a new job whenever the app pool recycles:

### App Pool Recycle Triggers
- Manual recycle
- Scheduled recycle (default: daily at 1:29 AM)
- Memory threshold reached
- Application code updated
- Configuration change

**In all cases:** The API will create a fresh job with 42,950 segments automatically.

### Important Note
⚠️ **Job state is lost on recycle!** 

The current implementation stores job state in memory. When the app pool recycles:
- Old job and progress is lost
- New job starts from zero
- Workers will start receiving segments from the new job

**For persistent jobs across restarts, you'd need to add database storage** (not currently implemented).

---

## Deployment Steps (Updated)

### Step 1: Deploy to Server

Copy the new `publish/api` folder to `192.168.1.9`:

```powershell
Copy-Item -Path "C:\Users\administrators1\source\repos\Seive2\publish\api\*" `
		  -Destination "\\192.168.1.9\c$\Apps\Seive2.Api\" `
		  -Recurse -Force
```

### Step 2: Start API on Server

On the server:
```powershell
cd C:\Apps\Seive2.Api
dotnet Seive2.Api.dll --urls "http://0.0.0.0:5150"
```

### Step 3: Verify Auto-Start

From your dev machine:
```powershell
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/status"
```

You should see `totalSegments: 42950` immediately!

### Step 4: Start Workers

Workers can now connect right away:
```powershell
cd Seive2.Worker
dotnet run -- http://192.168.1.9:5150
```

---

## Files Created/Modified

| File | Type | Description |
|------|------|-------------|
| **Seive2/HostedServices/AutoStartJobService.cs** | NEW | Auto-start background service |
| **Seive2/Program.cs** | MODIFIED | Registered auto-start service |
| **publish/api/** | UPDATED | New published API with auto-start |

---

## Testing the Feature

### Test 1: Start API and Check Logs
```powershell
dotnet run --project Seive2/Seive2.Api.csproj --urls "http://localhost:5150"
```

Look for:
```
✅ Job auto-started successfully!
Total Segments: 42,950
Ready for workers to connect
```

### Test 2: Check Job Status API
```powershell
Invoke-RestMethod -Uri "http://localhost:5150/api/job/status"
```

Should return job with `maxNumber: 4294967295`.

### Test 3: Start a Worker
```powershell
cd Seive2.Worker
dotnet run -- http://localhost:5150
```

Worker should immediately receive segments without you manually starting a job.

---

## Summary

✅ **Auto-start job on API startup:** Enabled  
✅ **Default max number:** 4,294,967,295 (uint.MaxValue)  
✅ **Default segment size:** 100,000  
✅ **Total segments:** 42,950  
✅ **Works on app pool recycle:** Yes  
✅ **Published to:** `publish/api/`  

**Deploy the new API and it will automatically start processing primes on every restart/recycle!**

---

## What About uint.MaxValue vs 100 Billion?

You asked for `uint.MaxValue` which is **4,294,967,295** (~4.3 billion).

If you actually wanted **100 billion**, edit `AutoStartJobService.cs`:

```csharp
const long maxNumber = 100_000_000_000; // 100 billion
```

**Current setting:** 4.3 billion (uint.MaxValue) with 42,950 segments  
**If changed to 100 billion:** Would create 1,000,000 segments

The current published API uses **uint.MaxValue** as requested.
