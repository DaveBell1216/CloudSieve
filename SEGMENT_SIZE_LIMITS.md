# Segment Size (Frame Size) Limits

## Quick Answer

### Segment Size Limits

| Limit Type | Value | Notes |
|------------|-------|-------|
| **Minimum** | `1,000` | Enforced by MemoryValidator |
| **Maximum** | `100,000,000` | 100 million - enforced by MemoryValidator |
| **Default** | `1,000,000` | 1 million (defined in StartJobRequest) |
| **Current auto-start** | `100,000` | 100 thousand |
| **Recommended** | `100,000` to `1,000,000` | Balance between memory and coordination |

---

## Detailed Breakdown

### Hard Limits in Code

**File:** `Seive2/Services/MemoryValidator.cs`

```csharp
private const long MAX_SEGMENT_SIZE = 100_000_000; // 100 million per segment

// Validation:
if (segmentSize < 1000)
	return (false, "SegmentSize must be at least 1000");

if (segmentSize > MAX_SEGMENT_SIZE)
	return (false, "SegmentSize cannot exceed 100,000,000 (100 million) due to memory constraints");
```

### Why 100 Million Maximum?

The segment size is limited by:

1. **Memory per segment:**
   - Uses `BitArray` for efficient storage (1 bit per number)
   - 100 million numbers = 100,000,000 bits = **12.5 MB per segment**
   - Safe to fit in memory

2. **Array indexing:**
   - Segment size must fit in `int` for array indexing
   - `int.MaxValue = 2,147,483,647`
   - 100 million is well below this limit

3. **Processing time:**
   - Larger segments take longer to process
   - Workers may time out or appear stuck
   - 100 million is a practical upper limit for responsiveness

---

## Segment Size Examples

### Small Segments (Fast Coordination, More Overhead)
```csharp
segmentSize = 10_000; // 10 thousand
```
- **Memory per segment:** ~1.25 KB
- **Segments for 100B:** 10 million segments
- **Worker coordination:** Very frequent API calls
- **Use case:** Testing, debugging

### Default (Balanced)
```csharp
segmentSize = 1_000_000; // 1 million (default)
```
- **Memory per segment:** ~125 KB
- **Segments for 100B:** 100,000 segments
- **Worker coordination:** Good balance
- **Use case:** General purpose, production

### Current Auto-Start (Good Performance)
```csharp
segmentSize = 100_000; // 100 thousand
```
- **Memory per segment:** ~12.5 KB
- **Segments for uint.MaxValue:** 42,950 segments
- **Worker coordination:** Frequent but manageable
- **Use case:** Your current configuration

### Large Segments (Less Coordination, Slower Workers)
```csharp
segmentSize = 10_000_000; // 10 million
```
- **Memory per segment:** ~1.25 MB
- **Segments for 100B:** 10,000 segments
- **Worker coordination:** Infrequent API calls
- **Use case:** Large scale with fewer workers

### Maximum Allowed (Not Recommended)
```csharp
segmentSize = 100_000_000; // 100 million (MAX)
```
- **Memory per segment:** ~12.5 MB
- **Segments for 100B:** 1,000 segments
- **Worker coordination:** Very infrequent
- **Processing time:** Each segment takes ~10-30 seconds
- **Use case:** Specialized scenarios only

---

## Memory Usage by Segment Size

| Segment Size | Memory per Segment | Segments for 100B | Total API Overhead |
|--------------|-------------------|-------------------|-------------------|
| 1,000 | ~125 bytes | 100,000,000 | Very high API traffic |
| 10,000 | ~1.25 KB | 10,000,000 | High API traffic |
| 100,000 | ~12.5 KB | 1,000,000 | Moderate (current) |
| 1,000,000 | ~125 KB | 100,000 | Low (default) |
| 10,000,000 | ~1.25 MB | 10,000 | Very low |
| 100,000,000 | ~12.5 MB | 1,000 | Minimal (max) |

---

## Choosing the Right Segment Size

### For Testing (Quick Results)
```csharp
maxNumber = 1_000_000;      // 1 million
segmentSize = 10_000;        // 10 thousand
// Result: 100 segments, finishes in seconds
```

### For Production (Balanced)
```csharp
maxNumber = 100_000_000_000; // 100 billion
segmentSize = 1_000_000;     // 1 million (default)
// Result: 100,000 segments, good balance
```

### For Maximum Throughput (Fewer but Larger)
```csharp
maxNumber = 100_000_000_000; // 100 billion
segmentSize = 10_000_000;    // 10 million
// Result: 10,000 segments, less API overhead
```

### For Current Auto-Start
```csharp
maxNumber = 4_294_967_295;   // uint.MaxValue
segmentSize = 100_000;       // 100 thousand
// Result: 42,950 segments, fast coordination
```

---

## Performance Implications

### Small Segments (< 100,000)

**Pros:**
- ✅ Fast processing per segment
- ✅ Workers finish quickly and get new work
- ✅ Fine-grained progress tracking
- ✅ Easy to distribute across many workers

**Cons:**
- ❌ High API coordination overhead
- ❌ Network latency becomes significant
- ❌ More segments = more database/memory for tracking
- ❌ Increased context switching

### Large Segments (> 1,000,000)

**Pros:**
- ✅ Less API overhead
- ✅ Fewer total segments to track
- ✅ Less network traffic
- ✅ Workers spend more time computing

**Cons:**
- ❌ Slower feedback per segment
- ❌ Workers may appear "stuck"
- ❌ Harder to balance load across workers
- ❌ If worker dies, lose more work

### Recommended Sweet Spot: 100,000 to 1,000,000

This range balances:
- Memory efficiency
- Processing time per segment
- API coordination overhead
- Progress visibility

---

## How to Change Segment Size

### Option 1: Change Auto-Start Default

**Edit:** `Seive2/HostedServices/AutoStartJobService.cs`

```csharp
public Task StartAsync(CancellationToken cancellationToken)
{
	const long maxNumber = uint.MaxValue;
	const long segmentSize = 1_000_000; // Change this (1,000 to 100,000,000)

	var job = _coordinator.StartJob(maxNumber, segmentSize);
	return Task.CompletedTask;
}
```

### Option 2: Manual Job Start

```powershell
$body = @{
	maxNumber = 100000000000
	segmentSize = 500000  # Half a million
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" `
				  -Method Post -Body $body -ContentType "application/json"
```

### Option 3: Change Default in StartJobRequest

**Edit:** `Seive2.Shared/Models/StartJobRequest.cs`

```csharp
public class StartJobRequest
{
	public long MaxNumber { get; set; }
	public long SegmentSize { get; set; } = 100_000; // Change default here
}
```

---

## Validation Rules

When you start a job, these checks run:

```csharp
✅ segmentSize >= 1,000           (minimum)
✅ segmentSize <= 100,000,000     (maximum)
✅ segmentSize divides work evenly (no validation, but better if it does)
```

---

## Examples by MaxNumber

### Small Job (Testing)
```csharp
maxNumber = 10_000_000;   // 10 million
segmentSize = 10_000;     // 10 thousand
// Segments: 1,000
// Time: ~1-2 minutes with 1 worker
```

### Medium Job (uint.MaxValue - Current Default)
```csharp
maxNumber = 4_294_967_295; // ~4.3 billion
segmentSize = 100_000;     // 100 thousand
// Segments: 42,950
// Time: ~15-30 minutes with 1 worker
```

### Large Job (100 Billion)
```csharp
maxNumber = 100_000_000_000; // 100 billion
segmentSize = 1_000_000;     // 1 million
// Segments: 100,000
// Time: ~30 hours with 1 worker, ~2 hours with 100 workers
```

### Huge Job (1 Trillion)
```csharp
maxNumber = 1_000_000_000_000; // 1 trillion
segmentSize = 10_000_000;      // 10 million
// Segments: 100,000
// Time: ~300 hours with 1 worker
```

---

## Trade-Off Summary

| Segment Size | API Calls | Memory | Processing Speed | Progress Granularity |
|--------------|-----------|--------|------------------|---------------------|
| 1,000 (min) | Very high | Tiny | Very fast | Very fine |
| 10,000 | High | Small | Fast | Fine |
| **100,000 (current)** | **Moderate** | **~12 KB** | **Good** | **Good** |
| **1,000,000 (default)** | **Low** | **~125 KB** | **Slower** | **Moderate** |
| 10,000,000 | Very low | ~1.25 MB | Slow | Coarse |
| 100,000,000 (max) | Minimal | ~12.5 MB | Very slow | Very coarse |

---

## Final Answer

### Maximum Segment Size (Frame Size)

✅ **Absolute Maximum:** `100,000,000` (100 million)

🎯 **Recommended Maximum:** `1,000,000` to `10,000,000` (1-10 million)

⚙️ **Current Auto-Start:** `100,000` (100 thousand)

📝 **Minimum:** `1,000` (1 thousand)

---

## To Change Current Auto-Start Segment Size

**Edit:** `Seive2/HostedServices/AutoStartJobService.cs`

```csharp
const long segmentSize = 100_000; // Change to any value between 1,000 and 100,000,000
```

Then republish:
```powershell
dotnet publish Seive2/Seive2.Api.csproj -c Release -o publish/api
```

**The segment size controls how much work each worker processes before reporting back to the API.**
