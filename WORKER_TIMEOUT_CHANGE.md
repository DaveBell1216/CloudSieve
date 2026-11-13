# Worker Timeout Changed to 600 Seconds

## What Changed

The worker timeout (stale segment timeout) has been increased from **5 minutes (300 seconds)** to **10 minutes (600 seconds)**.

---

## Location of Change

**File:** `Seive2/Services/WorkCoordinator.cs`  
**Line:** 199  
**Method:** `RequeueStaleSegments()`

### Before:
```csharp
var staleThreshold = DateTime.UtcNow.AddMinutes(-5);
```

### After:
```csharp
var staleThreshold = DateTime.UtcNow.AddMinutes(-10);
```

---

## What This Means

The API tracks when work segments are assigned to workers. If a worker:
- Receives a segment
- Takes longer than 10 minutes to complete it
- Doesn't submit the result

The API will consider that segment **stale** and reassign it to another worker.

### Old Behavior (5 minutes):
- Worker had to complete segment and submit result within 5 minutes
- If it took 6 minutes, segment would be reassigned to another worker
- Could cause duplicate work if workers were just slow

### New Behavior (10 minutes):
- Worker now has 10 minutes to complete and submit
- More tolerance for larger/slower segments
- Less likely to reassign work that's still being processed

---

## Why This Change?

With larger segment sizes or slower hardware:
- Segments can take 5-10 minutes to process
- Old 5-minute timeout was too aggressive
- Workers were having segments stolen before they could finish

**10 minutes provides better balance between:**
- ✅ Allowing workers enough time to complete large segments
- ✅ Detecting truly stuck/crashed workers
- ✅ Avoiding excessive duplicate work

---

## Deployment

The updated API has been published to:
```
C:\Users\administrators1\source\repos\Seive2\publish\api\
```

### Deploy to Server:
```powershell
# Copy to server
Copy-Item -Path "C:\Users\administrators1\source\repos\Seive2\publish\api\*" `
		  -Destination "\\192.168.1.9\c$\Apps\Seive2.Api\" `
		  -Recurse -Force

# On server, restart the API
# (or just restart the app pool if running in IIS)
```

---

## Impact

### Workers
- ✅ No changes needed to workers
- ✅ Workers get more time to complete segments
- ✅ Less likely to have work reassigned while processing

### API
- ✅ More lenient timeout
- ✅ Better handling of large segments
- ✅ Reduced duplicate work

### Performance
- ✅ Fewer false-positive stale detections
- ✅ More efficient use of worker time
- ⚠️ Crashed workers take 10 minutes to be detected (vs 5 minutes before)

---

## Related Timeouts

For reference, here are other timeout values in the system:

| Component | Timeout | Location | Purpose |
|-----------|---------|----------|---------|
| **Worker HTTP Client** | 10 minutes | `Seive2.Worker/Program.cs` line 26 | Worker request timeout |
| **Stale Segment Timeout** | **10 minutes** | `WorkCoordinator.cs` line 199 | **CHANGED** |
| **Worker "No Work" Wait** | 5 seconds | `Seive2.Worker/Program.cs` line 53 | Wait before retry |

---

## Testing

After deploying, verify the new timeout works:

### Test 1: Normal Operation
```powershell
# Start API with new timeout
cd C:\Apps\Seive2.Api
dotnet Seive2.Api.dll --urls "http://0.0.0.0:5150"

# Start a worker
cd Seive2.Worker
dotnet run -- http://192.168.1.9:5150
```

Worker should complete segments without issues.

### Test 2: Verify Stale Detection (Optional)
To test the 10-minute timeout:
1. Start a worker
2. Pause/suspend the worker process (Ctrl+Z or Task Manager)
3. Wait 11 minutes
4. Check API logs - should see segment reassigned
5. Resume worker - original work submission should be rejected (already completed by another worker)

---

## Summary

✅ **Timeout Changed:** 5 minutes → 10 minutes (600 seconds)  
✅ **File Modified:** `Seive2/Services/WorkCoordinator.cs`  
✅ **API Published:** Ready in `publish/api/`  
✅ **Workers:** No changes needed  
✅ **Benefit:** More time for workers to complete large segments  

**Deploy the updated API to activate the new 10-minute timeout!**
