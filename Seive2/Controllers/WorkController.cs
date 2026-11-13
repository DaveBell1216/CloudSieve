using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seive2.Shared.Models;
using Seive2.Services;

namespace Seive2.Controllers;

[ApiController]
[Authorize(Policy = "Sieve.Worker")]
[Route("api/[controller]")]
public class WorkController : ControllerBase
{
    private readonly SqlWorkCoordinator _coordinator;
    private readonly LargestPrimeTracker _primeTracker;
    private readonly ILogger<WorkController> _logger;

    public WorkController(SqlWorkCoordinator coordinator, LargestPrimeTracker primeTracker, ILogger<WorkController> logger)
    {
        _coordinator = coordinator;
        _primeTracker = primeTracker;
        _logger = logger;
    }

    /// <summary>
    /// Get next work segment(s) for a worker
    /// </summary>
    [HttpPost("next")]
    public async Task<ActionResult<object>> GetNextWork([FromBody] WorkRequest request)
    {
        var workerId = User.FindFirst("oid")?.Value;
        if (string.IsNullOrWhiteSpace(workerId))
        {
            return Unauthorized("The worker token must identify a managed identity.");
        }

        // Limit batch size to reasonable maximum
        int batchSize = Math.Clamp(request.BatchSize, 1, 10000);

        if (batchSize == 1)
        {
            // Single segment mode (backward compatible)
            var segment = (await _coordinator.GetNextWorkSegmentsAsync(workerId, 1, HttpContext.RequestAborted)).FirstOrDefault();

            if (segment == null)
            {
                return await NoWorkResponseAsync();
            }

            _logger.LogInformation("Assigned segment {SegmentId} ({Start}-{End}) to worker {WorkerId}",
                segment.SegmentId, segment.StartNumber, segment.EndNumber, workerId);

            return Ok(segment);
        }
        else
        {
            // Batch mode
            var segments = await _coordinator.GetNextWorkSegmentsAsync(workerId, batchSize, HttpContext.RequestAborted);

            if (segments.Count == 0)
            {
                return await NoWorkResponseAsync();
            }

            _logger.LogInformation("Assigned {Count} segments to worker {WorkerId} (requested {Requested})",
                segments.Count, workerId, batchSize);

            return Ok(segments);
        }
    }

    private async Task<ActionResult<object>> NoWorkResponseAsync()
    {
        var status = await _coordinator.GetJobStatusAsync(HttpContext.RequestAborted);
        return status?.Status is JobStatus.Completed or JobStatus.Failed
            ? StatusCode(StatusCodes.Status410Gone)
            : NoContent();
    }

    /// <summary>
    /// Submit completed work result
    /// </summary>
    [HttpPost("complete")]
    public async Task<IActionResult> CompleteWork([FromBody] WorkResult result)
    {
        var workerId = User.FindFirst("oid")?.Value;
        if (string.IsNullOrWhiteSpace(workerId))
        {
            return Unauthorized("The worker token must identify a managed identity.");
        }

        result.WorkerId = workerId;
        var success = await _coordinator.CompleteSegmentAsync(result, HttpContext.RequestAborted);

        if (!success)
        {
            return NotFound("Segment not found");
        }

        if (result.Success)
        {
            _logger.LogInformation("Worker {WorkerId} completed segment {SegmentId} with {Count} primes, largest: {Largest:N0}",
                result.WorkerId, result.SegmentId, result.PrimesFound, result.LargestPrimeFound);

            if (result.LargestPrimeFound == 0)
            {
                _logger.LogInformation("Segment {SegmentId} contained no primes", result.SegmentId);
            }
        }
        else
        {
            _logger.LogWarning("Worker {WorkerId} failed segment {SegmentId}: {Error}",
                result.WorkerId, result.SegmentId, result.ErrorMessage);
        }

        return Ok();
    }

    /// <summary>
    /// Get the current largest prime number found
    /// </summary>
    [HttpGet("largest-prime")]
    public async Task<ActionResult<long>> GetLargestPrime()
    {
        var largest = await _primeTracker.GetLargestPrimeAsync();
        return Ok(new { LargestPrime = largest });
    }
}
