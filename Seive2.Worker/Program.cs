using Azure.Core;
using Azure.Identity;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Seive2.Shared.Models;
using Seive2.Worker;

// Configuration
string serverUrl = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("SIEVE_SERVER_URL") ?? "http://localhost:5000";
string workerId = Environment.GetEnvironmentVariable("SIEVE_WORKER_ID") ?? Environment.MachineName;
string workerName = $"{workerId}-Worker";
int maxSegments = ReadPositiveInt("SIEVE_MAX_SEGMENTS", 10_000);
int batchSize = ReadPositiveInt("SIEVE_BATCH_SIZE", 1);
int segmentsProcessed = 0;
string apiScope = Environment.GetEnvironmentVariable("SIEVE_API_SCOPE")
    ?? throw new InvalidOperationException("SIEVE_API_SCOPE must be configured for Entra authentication.");
var credential = new DefaultAzureCredential();

if (string.IsNullOrWhiteSpace(workerId))
{
    throw new InvalidOperationException("SIEVE_WORKER_ID must not be empty.");
}

if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var serverUri) ||
    (serverUri.Scheme != Uri.UriSchemeHttp && serverUri.Scheme != Uri.UriSchemeHttps))
{
    throw new InvalidOperationException("The server URL must be an absolute HTTP or HTTPS URL.");
}

if (serverUri.Scheme != Uri.UriSchemeHttps && !serverUri.IsLoopback)
{
    throw new InvalidOperationException("The coordinator must use HTTPS except on loopback.");
}

if (batchSize > 10_000)
{
    throw new InvalidOperationException("SIEVE_BATCH_SIZE must not exceed 10000.");
}

Console.WriteLine("=========================================");
Console.WriteLine("  Distributed Prime Sieve - Worker Node");
Console.WriteLine("=========================================");
Console.WriteLine($"Worker ID: {workerId}");
Console.WriteLine($"Worker Name: {workerName}");
Console.WriteLine($"Server URL: {serverUrl}");
Console.WriteLine($"Max Segments: {maxSegments}");
Console.WriteLine($"Batch Size: {batchSize}");
Console.WriteLine("=========================================");
Console.WriteLine();

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(serverUrl),
    Timeout = TimeSpan.FromMinutes(10)
};

// Main worker loop
while (segmentsProcessed < maxSegments)
{
    //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DEBUG: Loop start - segmentsProcessed={segmentsProcessed}, maxSegments={maxSegments}");
    try
    {
        // Request next batch of work segments
        var workRequest = new WorkRequest
        {
            WorkerId = workerId,
            WorkerName = workerName,
            BatchSize = Math.Min(batchSize, maxSegments - segmentsProcessed)
        };

        await SetBearerTokenAsync(httpClient, credential, apiScope);
        var response = await httpClient.PostAsJsonAsync("/api/work/next", workRequest);

        if (response.StatusCode == System.Net.HttpStatusCode.Gone)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Job is complete. Worker exiting.");
            break;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            // No work available - check if we've completed our quota before waiting
            if (segmentsProcessed >= maxSegments)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] No more work available. Quota reached.");
                break; // Exit the loop
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] No work available. Waiting 5 seconds...");
            await Task.Delay(5000);
            continue;
        }

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException($"Coordinator rejected the worker identity: {response.StatusCode}");
        }

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Error requesting work: {response.StatusCode}");
            await Task.Delay(5000);
            continue;
        }

        // Try to parse as batch (List<WorkSegment>) first
        var content = await response.Content.ReadAsStringAsync();
        List<WorkSegment> segments;

        try
        {
            // Try to deserialize as array/list
            var segmentArray = JsonSerializer.Deserialize<List<WorkSegment>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            segments = segmentArray ?? new List<WorkSegment>();
        }
        catch
        {
            // Fallback: single segment (backward compatibility)
            var singleSegment = JsonSerializer.Deserialize<WorkSegment>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            segments = singleSegment != null ? new List<WorkSegment> { singleSegment } : new List<WorkSegment>();
        }

        if (segments.Count == 0)
        {
            await Task.Delay(5000);
            continue;
        }

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Received {segments.Count} segments from API");

        // Process each segment in the batch
        foreach (var segment in segments)
        {
            if (segmentsProcessed >= maxSegments)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Quota reached, skipping remaining segments in batch");
                break;
            }

            //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Processing segment {segmentsProcessed + 1}/{maxSegments}: {segment.StartNumber:N0} - {segment.EndNumber:N0}");

            // Process the segment
            var startTime = DateTime.Now;
            long primesFound = 0;
            long largestPrime = 0;
            bool success = true;
            string? errorMessage = null;

            try
            {
                var result = PrimeSieve.FindPrimesInRange(segment.StartNumber, segment.EndNumber);
                primesFound = result.Count;
                largestPrime = result.LargestPrime;
                var elapsed = DateTime.Now - startTime;
                //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Completed in {elapsed.TotalSeconds:F2}s - Found {primesFound:N0} primes");
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
            }

            // Submit result
            var workResult = new WorkResult
            {
                SegmentId = segment.SegmentId,
                AssignmentId = segment.AssignmentId,
                WorkerId = workerId,
                PrimesFound = primesFound,
                LargestPrimeFound = largestPrime,
                Success = success,
                ErrorMessage = errorMessage
            };

            await SetBearerTokenAsync(httpClient, credential, apiScope);
            var submitResponse = await httpClient.PostAsJsonAsync("/api/work/complete", workResult);
            if (submitResponse.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                throw new UnauthorizedAccessException($"Coordinator rejected the worker identity: {submitResponse.StatusCode}");
            }

            if (submitResponse.IsSuccessStatusCode && !success)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Failed segment returned to the queue; stopping this worker.");
                Environment.ExitCode = 1;
                return;
            }

            if (submitResponse.IsSuccessStatusCode)
            {
                segmentsProcessed++; // Increment counter on successful submission
                //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Result submitted successfully ({segmentsProcessed}/{maxSegments} completed)");

                // Check if we've reached the limit
                if (segmentsProcessed >= maxSegments)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ Segment limit reached ({maxSegments} segments completed)");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DEBUG: Breaking from loop after successful submission");
                    break; // Exit the foreach loop
                }
                else
                {
                    //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DEBUG: Continuing - need {maxSegments - segmentsProcessed} more segments");
                }
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Warning: Failed to submit result: {submitResponse.StatusCode}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Segment not counted towards quota due to submission failure");
            }

            //Console.WriteLine();
        }

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DEBUG: Last EndNumber processed: {string.Format("{0:N0}", segments.OrderBy(x => x.EndNumber).LastOrDefault()?.EndNumber)}");

        // Check again if we should exit the outer loop
        if (segmentsProcessed >= maxSegments)
        {
            break;
        }
    }
    catch (UnauthorizedAccessException)
    {
        throw;
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Network error: {ex.Message}");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DEBUG: In HttpRequestException handler - segmentsProcessed={segmentsProcessed}");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Retrying in 10 seconds...");
        await Task.Delay(10000);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Unexpected error: {ex.Message}");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DEBUG: In Exception handler - segmentsProcessed={segmentsProcessed}");
        await Task.Delay(5000);
    }
}

Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DEBUG: Exited main loop - segmentsProcessed={segmentsProcessed}");

// Worker completed its quota
Console.WriteLine("=========================================");
Console.WriteLine($"✅ Worker completed {segmentsProcessed} segments");
Console.WriteLine("Worker terminating...");
Console.WriteLine("=========================================");

static async Task SetBearerTokenAsync(HttpClient client, TokenCredential credential, string scope)
{
    var token = await credential.GetTokenAsync(new TokenRequestContext([scope]), CancellationToken.None);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
}

static int ReadPositiveInt(string name, int defaultValue)
{
    string? value = Environment.GetEnvironmentVariable(name);
    if (value is null)
    {
        return defaultValue;
    }

    if (!int.TryParse(value, out int result) || result <= 0)
    {
        throw new InvalidOperationException($"{name} must be a positive integer.");
    }

    return result;
}
