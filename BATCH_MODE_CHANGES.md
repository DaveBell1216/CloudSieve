# Batch Mode Implementation - 1000 Segments per Request

## Overview
Updated the distributed prime sieve to support **batch segment requests**, allowing workers to request up to 1000 segments at a time instead of requesting them one-by-one.

## Changes Made

### 1. **Seive2.Shared/Models/WorkRequest.cs**
- Added `BatchSize` property (default: 1, max: 10,000)
- Workers can now specify how many segments they want per API request

### 2. **Seive2/Services/WorkCoordinator.cs**
- Added new `GetNextWorkSegments()` method to support batch dequeuing
- Modified existing `GetNextWorkSegment()` to call the batch method with size=1
- Batch method efficiently dequeues multiple segments in one operation
- Includes stale segment requeuing if initial batch is empty

### 3. **Seive2/Controllers/WorkController.cs**
- Updated `GetNextWork()` endpoint to support both single and batch modes
- Single segment mode (BatchSize=1): returns `WorkSegment` object (backward compatible)
- Batch mode (BatchSize>1): returns `List<WorkSegment>` array
- Added batch size clamping (1-10,000) for safety
- Enhanced logging to show batch counts

### 4. **Seive2.Worker/Program.cs**
- Set `batchSize = 1000` to request 1000 segments per API call
- Updated request logic to include `BatchSize` in `WorkRequest`
- Added smart deserialization to handle both single segment and batch responses
- Worker now processes segments in a batch loop:
  - Requests 1000 segments from API
  - Processes each segment individually
  - Submits results one-by-one
  - Still respects `maxSegments` quota (default: 25)
- Enhanced console output to show batch sizes

## Benefits

1. **Reduced Network Overhead**: 1000x fewer HTTP requests to API
2. **Lower API Load**: Fewer endpoint hits and less serialization overhead
3. **Improved Throughput**: Workers spend less time waiting on network I/O
4. **Backward Compatible**: Single-segment mode still works (BatchSize=1)
5. **Configurable**: Batch size can be adjusted per worker without API changes

## Configuration

### API
- Maximum batch size is capped at **10,000 segments** in `WorkController`
- Can be adjusted in `WorkController.GetNextWork()` clamp logic

### Worker
- Current batch size: **1000 segments** (`batchSize` variable)
- Current segment quota: **25 segments** (`maxSegments` variable)
- Server URL: configurable via command-line argument

## Deployment

Published artifacts are ready in:
- **API**: `publish/api/`
- **Worker**: `publish/worker/`

### To Deploy on Server (192.168.1.9)
1. Stop the existing API service/app pool
2. Copy `publish/api/*` to server API directory
3. Restart API service/app pool (will auto-start job via `AutoStartJobService`)
4. Copy `publish/worker/*` to server worker directory
5. Run worker: `Seive2.Worker.exe http://192.168.1.9:5150`

## Example Behavior

**Before (single segment mode):**
- Worker requests 1 segment → API responds → Worker processes → Worker submits result
- **25 segments = 50 HTTP requests** (25 request + 25 submit)

**After (batch mode):**
- Worker requests 1000 segments → API responds with 1000 → Worker processes all 1000 → Worker submits 1000 results
- **25 segments = 26 HTTP requests** (1 batch request + 25 submits)

If worker quota is 25 segments:
- Worker requests 1000 segments
- Receives 1000 segments from API
- Processes first 25 segments
- Stops and exits (quota reached)
- Remaining 975 segments become "stale" and requeue after 10 minutes

## Testing

Build succeeded ✅  
All projects compiled successfully with new batch logic.
