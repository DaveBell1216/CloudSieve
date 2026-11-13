# Quick Reference - Auto-Start API

## ✅ What's New

**The API now auto-starts a job on every startup/recycle!**

---

## Current Auto-Start Settings

| Setting | Value |
|---------|-------|
| Max Number | 4,294,967,295 (uint.MaxValue) |
| Segment Size | 100,000 |
| Total Segments | 42,950 |
| Auto-start on recycle | ✅ Yes |

---

## Deploy to Server

```powershell
# 1. Copy to server
Copy-Item -Path "publish\api\*" -Destination "\\192.168.1.9\c$\Apps\Seive2.Api\" -Recurse -Force

# 2. On server, start API
cd C:\Apps\Seive2.Api
dotnet Seive2.Api.dll --urls "http://0.0.0.0:5150"
```

**That's it!** Job auto-starts. No manual `/api/job/start` needed.

---

## Verify from Dev Machine

```powershell
# Check job auto-started
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/status"
```

Should show:
- `maxNumber: 4294967295`
- `totalSegments: 42950`
- `status: 1` (Running)

---

## Start Workers Immediately

```powershell
cd Seive2.Worker
dotnet run -- http://192.168.1.9:5150
```

Workers can connect right away and start processing!

---

## Change Auto-Start Settings (Optional)

Edit: `Seive2/HostedServices/AutoStartJobService.cs`

```csharp
const long maxNumber = uint.MaxValue;  // Change this
const long segmentSize = 100_000;      // Change this
```

Then republish:
```powershell
dotnet publish Seive2/Seive2.Api.csproj -c Release -o publish/api
```

---

## App Pool Recycle Behavior

✅ **Every recycle = new job**  
⚠️ **Old job progress is lost** (in-memory only, no persistence)

---

## Files Ready for Deployment

```
C:\Users\administrators1\source\repos\Seive2\publish\api\
```

**Copy this entire folder to the server and run!**
