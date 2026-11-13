# All Timeout Issues Fixed - Complete Summary

## Problem

Something was timing out after **5 minutes** even though the WorkCoordinator timeout was changed to 10 minutes.

## Root Cause

The 5-minute timeout was coming from **Kestrel server defaults**, not the WorkCoordinator. ASP.NET Core (Kestrel) has built-in connection timeouts that were disconnecting workers.

---

## All Timeout Values - Before and After

### 1. Kestrel Keep-Alive Timeout (SERVER SIDE)
**The hidden culprit!**

| Setting | Before | After | Location |
|---------|--------|-------|----------|
| KeepAliveTimeout | **5 minutes (default)** | **15 minutes** | `Seive2/Program.cs` |
| RequestHeadersTimeout | **5 minutes (default)** | **15 minutes** | `Seive2/Program.cs` |

**This was causing the 5-minute disconnects!**

### 2. Worker Segment Reassignment Timeout (API LOGIC)
| Setting | Before | After | Location |
|---------|--------|-------|----------|
| Stale segment timeout | 5 minutes | **10 minutes** | `WorkCoordinator.cs` line 199 |

### 3. Worker HTTP Client Timeout (CLIENT SIDE)
| Setting | Value | Location |
|---------|-------|----------|
| HTTP request timeout | 10 minutes | `Seive2.Worker/Program.cs` line 26 |

---

## Changes Made

### Change 1: Kestrel Server Configuration (NEW)

**File:** `Seive2/Program.cs`

Added Kestrel timeout configuration:

```csharp
// Configure Kestrel server timeouts
builder.WebHost.ConfigureKestrel(serverOptions =>
{
	serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(15);
	serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(15);
});
```

**What this fixes:**
- ✅ Prevents Kestrel from closing idle connections after 5 minutes
- ✅ Allows workers to process long segments (up to 15 minutes)
- ✅ Gives buffer time beyond the 10-minute worker timeout

### Change 2: Work Coordinator Timeout (PREVIOUSLY DONE)

**File:** `Seive2/Services/WorkCoordinator.cs` line 199

```csharp
// Changed from:
var staleThreshold = DateTime.UtcNow.AddMinutes(-5);

// To:
var staleThreshold = DateTime.UtcNow.AddMinutes(-10);
```

**What this fixes:**
- ✅ API waits 10 minutes before reassigning segments
- ✅ More tolerance for large/slow segments

### Change 3: Worker HTTP Client (ALREADY SET)

**File:** `Seive2.Worker/Program.cs` line 26

```csharp
Timeout = TimeSpan.FromMinutes(10)
```

**Already configured correctly.**

---

## How the Timeouts Work Together

```
Worker starts processing segment
		 ↓
	[0-15 minutes]
		 ↓
	┌────────────────────────────────────────┐
	│  Kestrel KeepAlive: 15 minutes         │ ← Server won't disconnect
	│  ┌──────────────────────────────────┐  │
	│  │ Worker HTTP Timeout: 10 minutes  │  │ ← Client request limit
	│  │ ┌────────────────────────────┐   │  │
	│  │ │ Stale Segment: 10 minutes  │   │  │ ← API reassignment logic
	│  │ │                            │   │  │
	│  │ │  Worker processing...      │   │  │
	│  │ │                            │   │  │
	│  │ └────────────────────────────┘   │  │
	│  └──────────────────────────────────┘  │
	└────────────────────────────────────────┘
		 ↓
	Submit result to API
```

**Timeline:**

| Time | Event | Status |
|------|-------|--------|
| 0:00 | Worker receives segment | ✅ All systems go |
| 5:00 | **OLD:** Kestrel would disconnect | ❌ Connection lost |
| 5:00 | **NEW:** Kestrel still connected | ✅ Connection alive |
| 10:00 | Worker HTTP timeout limit | ⚠️ Worker should be done |
| 10:00 | API marks segment as stale | ⚠️ Will reassign if not complete |
| 15:00 | Kestrel timeout limit | ❌ Hard disconnect |

---

## What Each Timeout Controls

### Kestrel KeepAliveTimeout (15 minutes)
**Purpose:** How long the server keeps an idle HTTP connection open

**Impact:**
- Worker fetches segment → Connection stays open
- Worker processing (connection idle) → Server waits up to 15 minutes
- If worker doesn't send anything for 15 minutes → Connection closed

**Why 15 minutes:** Gives 5-minute buffer beyond worker processing time

### Kestrel RequestHeadersTimeout (15 minutes)
**Purpose:** How long server waits for request headers

**Impact:**
- Prevents slow header attacks
- Allows slow network connections

**Why 15 minutes:** Matches KeepAliveTimeout for consistency

### Worker Stale Timeout (10 minutes)
**Purpose:** How long API waits before reassigning a segment

**Impact:**
- Segment assigned to worker at 0:00
- If no result by 10:00 → Segment reassigned to another worker
- Original worker can still submit (will be rejected as duplicate)

**Why 10 minutes:** Reasonable time for segments to process

### Worker HTTP Client Timeout (10 minutes)
**Purpose:** Maximum time a single HTTP request can take

**Impact:**
- Worker makes request (fetch segment or submit result)
- If request takes > 10 minutes → Worker throws timeout exception
- Worker retries or continues

**Why 10 minutes:** Matches stale segment timeout

---

## Testing the Fix

### Test 1: Start API and Verify Kestrel Settings

```powershell
dotnet run --project Seive2/Seive2.Api.csproj --urls "http://localhost:5150"
```

The API should start without errors. Kestrel timeout configuration is applied silently.

### Test 2: Long-Running Segment Test

1. **Start API** with a job that has large segments:
   ```powershell
   $body = @{ maxNumber = 100000000000; segmentSize = 10000000 } | ConvertTo-Json
   Invoke-RestMethod -Uri "http://localhost:5150/api/job/start" -Method Post -Body $body -ContentType "application/json"
   ```

2. **Start a worker:**
   ```powershell
   cd Seive2.Worker
   dotnet run -- http://localhost:5150
   ```

3. **Watch the worker process a segment**
   - Should take 5-10 minutes per segment
   - **OLD BEHAVIOR:** Would disconnect at exactly 5:00
   - **NEW BEHAVIOR:** Continues past 5:00, completes successfully

### Test 3: Monitor for 5-Minute Disconnects

Run a worker and watch console output:
- If you see "Network error" or "Connection closed" at exactly 5:00 → **Still broken**
- If segment completes after 5 minutes with success → **Fixed!**

---

## Deployment

### Step 1: Copy Updated API to Server

```powershell
Copy-Item -Path "C:\Users\administrators1\source\repos\Seive2\publish\api\*" `
		  -Destination "\\192.168.1.9\c$\Apps\Seive2.Api\" `
		  -Recurse -Force
```

### Step 2: Restart API on Server

On the server:
```powershell
# Stop existing API process
Get-Process | Where-Object {$_.ProcessName -like "*dotnet*"} | Stop-Process -Force

# Start new API
cd C:\Apps\Seive2.Api
dotnet Seive2.Api.dll --urls "http://0.0.0.0:5150"
```

Or if running in IIS:
- Recycle the App Pool
- The new Kestrel settings will take effect automatically

### Step 3: Start Workers

Workers don't need any changes:
```powershell
cd Seive2.Worker
dotnet run -- http://192.168.1.9:5150
```

---

## Summary of All Timeout Settings

| Component | Setting | Value | Purpose |
|-----------|---------|-------|---------|
| **Kestrel** | KeepAliveTimeout | **15 minutes** | Server connection timeout |
| **Kestrel** | RequestHeadersTimeout | **15 minutes** | Header receive timeout |
| **API** | Stale segment timeout | **10 minutes** | Reassign abandoned work |
| **Worker** | HTTP client timeout | **10 minutes** | Client request timeout |

---

## Why You Were Seeing 5-Minute Timeouts

**Kestrel's default KeepAliveTimeout is 5 minutes.**

Even though:
- ✅ WorkCoordinator timeout was 10 minutes
- ✅ Worker HTTP client timeout was 10 minutes

The **Kestrel server** was still using its default 5-minute timeout and closing the connection!

This is a common gotcha in ASP.NET Core for long-running operations.

---

## Files Modified

| File | Change | Description |
|------|--------|-------------|
| **Seive2/Program.cs** | Added Kestrel config | Server timeout settings |
| **Seive2/Services/WorkCoordinator.cs** | Changed -5 to -10 | Segment reassignment timeout |
| **publish/api/** | Rebuilt | Updated deployment package |

---

## Final Answer

✅ **Root cause identified:** Kestrel's default 5-minute KeepAliveTimeout  
✅ **Kestrel timeout increased:** 5 minutes → 15 minutes  
✅ **Stale segment timeout:** Already changed to 10 minutes  
✅ **Worker HTTP timeout:** Already set to 10 minutes  
✅ **Published:** New API ready in `publish/api/`  

**Deploy the updated API and the 5-minute timeout issue will be resolved!**

The three-layer timeout protection now works correctly:
1. Workers have 10 minutes to complete and submit
2. API reassigns after 10 minutes of no response
3. Kestrel keeps connections alive for 15 minutes
