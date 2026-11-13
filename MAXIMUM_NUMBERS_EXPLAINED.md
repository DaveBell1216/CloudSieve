# Maximum Numbers in the Prime Sieve System

## Direct Answer

The **largest number that a worker will test for primality** depends on the job configuration, but here are the absolute limits:

### Maximum Limits

| Limit Type | Value | Reason |
|------------|-------|--------|
| **Maximum Job Number** | 4,000,000,000,000,000,000 (4 × 10^18) | Algorithm & memory constraints |
| **Maximum Segment End** | Up to MaxNumber | Last segment ends at MaxNumber |
| **Maximum Segment Size** | 100,000,000 (100 million) | Memory constraints (array size) |
| **Practical Worker Range** | StartNumber to EndNumber | Worker processes one segment at a time |

## How It Works

### Segment Distribution

When you start a job with `maxNumber = 10,000,000` and `segmentSize = 1,000,000`:

```
Job: Find primes from 2 to 10,000,000
Divided into segments:

Segment 1:   2 to 1,000,001
Segment 2:   1,000,002 to 2,000,001
Segment 3:   2,000,002 to 3,000,001
...
Segment 10:  9,000,002 to 10,000,000  ← Last segment ends at MaxNumber
```

**Each worker tests all numbers in their assigned segment for primality.**

### Code Reference

From `WorkCoordinator.cs` (line 174-191):

```csharp
private List<WorkSegment> CreateSegments(long maxNumber, long segmentSize)
{
	var segments = new List<WorkSegment>();

	// Start from 2 (first prime)
	for (long start = 2; start <= maxNumber; start += segmentSize)
	{
		var end = Math.Min(start + segmentSize - 1, maxNumber);
		segments.Add(new WorkSegment
		{
			SegmentId = Guid.NewGuid(),
			StartNumber = start,
			EndNumber = end,  ← Worker tests up to this number
			Status = SegmentStatus.Pending
		});
	}

	return segments;
}
```

## Validation Limits (from MemoryValidator.cs)

### 1. MaxNumber Limit: 4 × 10^18
```csharp
private const long MAX_SAFE_NUMBER = 4_000_000_000_000_000_000L;
```

**Why this limit?**
- Requires generating base primes up to √maxNumber
- For 4×10^18, sqrt = 2×10^9 (just under int.MaxValue)
- Larger values would exceed array size limits

### 2. Segment Size Limit: 100,000,000
```csharp
private const long MAX_SEGMENT_SIZE = 100_000_000;
```

**Why this limit?**
- Each segment is processed using a BitArray
- Arrays in .NET are limited by int.MaxValue (~2.1 billion)
- 100 million is a safe practical limit for memory usage

### 3. Base Prime Limit: 2,000,000,000
```csharp
private const long MAX_BASE_PRIME_LIMIT = 2_000_000_000L;
```

**Why this limit?**
- Base primes are needed up to √maxNumber
- Must fit in int-indexed arrays
- Ensures we stay safely under int.MaxValue (2,147,483,647)

## Example Scenarios

### Small Job (Testing)
```json
{
  "maxNumber": 1000000,
  "segmentSize": 100000
}
```
- **Workers will test**: Up to 1,000,000
- **Segments**: 10
- **Largest segment**: 900,001 to 1,000,000

### Medium Job
```json
{
  "maxNumber": 1000000000,
  "segmentSize": 10000000
}
```
- **Workers will test**: Up to 1,000,000,000 (1 billion)
- **Segments**: 100
- **Largest segment**: 990,000,001 to 1,000,000,000

### Large Job (Near Maximum)
```json
{
  "maxNumber": 1000000000000000,
  "segmentSize": 100000000
}
```
- **Workers will test**: Up to 1,000,000,000,000,000 (1 quadrillion)
- **Segments**: 10,000,000
- **Largest segment**: 999,999,900,000,001 to 1,000,000,000,000,000

### At The Absolute Limit
```json
{
  "maxNumber": 4000000000000000000,
  "segmentSize": 100000000
}
```
- **Workers will test**: Up to 4,000,000,000,000,000,000 (4 quintillion)
- **Segments**: 40,000,000,000 (40 billion segments!)
- **Largest segment**: Last 100M numbers up to 4×10^18

## What Numbers Does Each Worker Actually Test?

Each worker:
1. **Receives one segment** with `StartNumber` and `EndNumber`
2. **Tests every number** in that range for primality
3. **Returns the count** of primes found in that segment

From the worker output you saw earlier:
```
[16:09:30] Received segment: 2 - 1,000,001
		   ↑ Worker tests all numbers from 2 to 1,000,001
[16:09:30] Completed in 0.01s - Found 78,498 primes
		   ↑ Found 78,498 primes in this range

[16:09:30] Received segment: 9,000,002 - 10,000,000
		   ↑ Testing the highest segment (last 1M numbers)
[16:09:30] Completed in 0.01s - Found 62,090 primes
```

## Testing Edge Cases

### Test Maximum Segment Size
```powershell
$body = @{ 
	maxNumber = 100000000
	segmentSize = 100000000  # Maximum allowed
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" `
	-Method POST -Body $body -ContentType "application/json"
```
Worker will test: **2 to 100,000,000** (single segment)

### Test Near Segment Size Limit
```powershell
$body = @{ 
	maxNumber = 100000001
	segmentSize = 100000000
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" `
	-Method POST -Body $body -ContentType "application/json"
```
Creates 2 segments:
- Segment 1: 2 to 100,000,001
- Segment 2: 100,000,002 to 100,000,001 (just 1 number!)

## Summary

**Direct Answer:**
- **Smallest number tested**: Always **2** (first prime)
- **Largest number tested**: Equal to your **MaxNumber** (up to 4×10^18)
- **Per-segment maximum**: **100 million** numbers at once
- **Worker tests**: Every integer in their assigned segment's range

**The API doesn't limit individual primes, it limits:**
1. The overall job size (MaxNumber ≤ 4×10^18)
2. The segment chunk size (≤ 100 million per segment)
3. The memory required for processing

**Each worker processes complete ranges** from StartNumber to EndNumber, testing every integer in that range for primality.
