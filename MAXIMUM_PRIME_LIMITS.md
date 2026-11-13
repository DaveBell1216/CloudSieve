# Maximum Prime Number Limits

## Quick Answer

### Current Hard Limits

| Limit Type | Value | Constraint |
|------------|-------|------------|
| **Absolute Maximum** | `9,223,372,036,854,775,807` | `long.MaxValue` (C# data type limit) |
| **Practical Maximum** | `4,000,000,000,000,000,000` | `4 × 10^18` (4 quintillion) - Enforced by MemoryValidator |
| **Safe Working Maximum** | `~4,000,000,000,000,000,000` | Based on sqrt memory constraints |

### Current Auto-Start Default
- **Auto-start uses:** `4,294,967,295` (uint.MaxValue, ~4.3 billion)

---

## Detailed Breakdown

### 1. Data Type Limit: `long.MaxValue`

Your code uses `long` for MaxNumber:
```csharp
public long MaxNumber { get; set; }
```

**Theoretical maximum:** `9,223,372,036,854,775,807` (9.2 quintillion)

This is the absolute ceiling - you cannot go higher without changing the data type.

---

### 2. Practical Limit: Memory Constraints

The **MemoryValidator** enforces a practical limit based on memory requirements.

#### Key Constraint: Base Prime Generation

The segmented sieve algorithm needs to generate all primes up to **√(MaxNumber)** first.

**Current limits in code:**
```csharp
private const long MAX_SAFE_NUMBER = 4_000_000_000_000_000_000L; // 4 × 10^18
private const long MAX_BASE_PRIME_LIMIT = 2_000_000_000L; // 2 billion
```

#### Why the sqrt(MaxNumber) Limit?

| MaxNumber | sqrt(MaxNumber) | Base Prime Array Size | Feasible? |
|-----------|-----------------|----------------------|-----------|
| 4,294,967,295 (4.3B) | 65,536 | ~8 KB | ✅ Easy |
| 100,000,000,000 (100B) | 316,228 | ~40 KB | ✅ Easy |
| 4,000,000,000,000,000,000 (4×10^18) | 2,000,000,000 | ~250 MB | ✅ Max safe |
| 9,000,000,000,000,000,000 (9×10^18) | 3,000,000,000 | **Exceeds int.MaxValue** | ❌ Won't work |

**The critical constraint:** 
- sqrt(MaxNumber) must be ≤ 2,147,483,647 (int.MaxValue)
- This limits MaxNumber to approximately **4.6 × 10^18**

---

### 3. Validation Rules

When you start a job, **MemoryValidator** checks:

```csharp
// 1. MaxNumber must be at least 2
if (maxNumber < 2) → FAIL

// 2. MaxNumber cannot exceed 4 × 10^18
if (maxNumber > 4_000_000_000_000_000_000L) → FAIL

// 3. sqrt(MaxNumber) cannot exceed 2 billion
long sqrtMaxNumber = (long)Math.Sqrt(maxNumber);
if (sqrtMaxNumber > 2_000_000_000L) → FAIL

// 4. Base prime memory cannot exceed 4 GB
if (estimatedMemory > 4096 MB) → FAIL

// 5. Segment size must be 1,000 to 100,000,000
if (segmentSize < 1000 || segmentSize > 100_000_000) → FAIL
```

---

## Practical Examples

### Allowed Values

| MaxNumber | Description | sqrt(MaxNumber) | Base Memory | Total Segments (100k seg) | Status |
|-----------|-------------|-----------------|-------------|---------------------------|--------|
| 4,294,967,295 | uint.MaxValue | 65,536 | ~0.5 MB | 42,950 | ✅ Current default |
| 100,000,000,000 | 100 billion | 316,228 | ~2 MB | 1,000,000 | ✅ Works great |
| 1,000,000,000,000 | 1 trillion | 1,000,000 | ~125 MB | 10,000,000 | ✅ Works |
| 1,000,000,000,000,000 | 1 quadrillion | 31,622,777 | ~500 MB | 10,000,000,000 | ✅ Works |
| 4,000,000,000,000,000,000 | 4 quintillion | 2,000,000,000 | ~4 GB | 40,000,000,000,000 | ✅ Max safe |

### Rejected Values

| MaxNumber | Issue | Why It Fails |
|-----------|-------|--------------|
| 5,000,000,000,000,000,000 | sqrt = 2,236,067,977 | Exceeds MAX_BASE_PRIME_LIMIT (2B) |
| 10,000,000,000,000,000,000 | sqrt = 3,162,277,660 | Exceeds int.MaxValue for array indexing |
| 9,223,372,036,854,775,807 | sqrt = 3,037,000,499 | Way over memory limits |

---

## How to Use Different Maximum Values

### Option 1: Change Auto-Start Default

Edit: `Seive2/HostedServices/AutoStartJobService.cs`

```csharp
public Task StartAsync(CancellationToken cancellationToken)
{
	// Change these values:
	const long maxNumber = 1_000_000_000_000L; // 1 trillion
	const long segmentSize = 100_000;

	var job = _coordinator.StartJob(maxNumber, segmentSize);
	return Task.CompletedTask;
}
```

### Option 2: Start Job Manually

From PowerShell:
```powershell
# Try 1 trillion
$body = @{
	maxNumber = 1000000000000
	segmentSize = 100000
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" `
				  -Method Post -Body $body -ContentType "application/json"
```

### Option 3: Modify Limits (Advanced)

Edit: `Seive2/Services/MemoryValidator.cs`

```csharp
// Increase the safe limit (be careful!)
private const long MAX_SAFE_NUMBER = 9_000_000_000_000_000_000L; // Will likely fail
private const long MAX_BASE_PRIME_LIMIT = 3_000_000_000L; // Exceeds int.MaxValue!
```

⚠️ **Warning:** Increasing these limits can cause runtime failures!

---

## Recommended Maximum Values by Use Case

### Conservative (Always Safe)
```csharp
const long maxNumber = 100_000_000_000L; // 100 billion
```
- Memory: ~2 MB for base primes
- Segments: 1 million (100k each)
- Workers needed (25/worker): 40,000

### Aggressive (High Memory)
```csharp
const long maxNumber = 1_000_000_000_000_000L; // 1 quadrillion
```
- Memory: ~500 MB for base primes
- Segments: 10 billion (100k each)
- Workers needed: 400 million (!!)

### Maximum Safe (Do Not Exceed)
```csharp
const long maxNumber = 4_000_000_000_000_000_000L; // 4 quintillion
```
- Memory: ~4 GB for base primes
- Segments: 40 trillion (100k each)
- Workers needed: 1.6 trillion (!!!)

---

## Why Not Just Use long.MaxValue?

**You cannot use `9,223,372,036,854,775,807` because:**

1. **sqrt(long.MaxValue) = 3,037,000,499**
   - This exceeds int.MaxValue (2,147,483,647)
   - Cannot create arrays larger than int.MaxValue in C#
   - Base prime generation will crash

2. **Memory requirements:**
   - Would need ~380 MB just for the base prime BitArray
   - Plus ~24 GB for the base prime list itself
   - Not practical

3. **Time to complete:**
   - At 1 second per segment (100k numbers), processing long.MaxValue would take:
   - **92,233,720,368,548 segments**
   - **2.9 billion years** with one worker
   - **2,900 years** with 1 million parallel workers

---

## Summary Table

| Purpose | Recommended MaxNumber | Notation |
|---------|----------------------|----------|
| **Quick Test** | 10,000,000 | 10 million |
| **Current Default** | 4,294,967,295 | uint.MaxValue (~4.3 billion) |
| **Practical Large** | 100,000,000,000 | 100 billion |
| **Very Large** | 1,000,000,000,000 | 1 trillion |
| **Huge** | 1,000,000,000,000,000 | 1 quadrillion |
| **Absolute Safe Maximum** | 4,000,000,000,000,000,000 | 4 quintillion (4×10^18) |
| **Hard Data Type Limit** | 9,223,372,036,854,775,807 | long.MaxValue (won't work) |

---

## Check Your Current Limit

Run this to see the validation result for any number:

```powershell
# Test if a value is valid
$body = @{ maxNumber = 1000000000000; segmentSize = 100000 } | ConvertTo-Json
Invoke-RestMethod -Uri "http://192.168.1.9:5150/api/job/start" `
				  -Method Post -Body $body -ContentType "application/json"
```

If it succeeds, you'll get a job ID. If it fails, you'll get an error message explaining why.

---

## Final Answer

✅ **Maximum you can safely use:** `4,000,000,000,000,000,000` (4 quintillion)

🎯 **Recommended maximum:** `100,000,000,000` (100 billion) - practical and fast

⚙️ **Current auto-start default:** `4,294,967,295` (4.3 billion) - easy and proven

**To use a different maximum, edit `AutoStartJobService.cs` and change the `maxNumber` constant.**
