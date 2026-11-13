using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seive2.Shared.Models;
using Seive2.Services;

namespace Seive2.Controllers;

[ApiController]
[Authorize(Policy = "Sieve.Operator")]
[Route("api/[controller]")]
public class JobController : ControllerBase
{
    private readonly SqlWorkCoordinator _coordinator;
    private readonly ILogger<JobController> _logger;

    public JobController(SqlWorkCoordinator coordinator, ILogger<JobController> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    /// <summary>
    /// Start a new prime sieve job
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult<SieveJob>> StartJob([FromBody] StartJobRequest request)
    {
        try
        {
            // Validate memory requirements and safety limits
            var (isValid, errorMessage) = MemoryValidator.ValidateJobParameters(
                request.MaxNumber, 
                request.SegmentSize);

            if (!isValid)
            {
                _logger.LogWarning("Job validation failed: {Error}", errorMessage);
                return BadRequest(errorMessage);
            }

            // Log memory estimates
            var memoryEstimate = MemoryValidator.EstimateMemoryUsage(
                request.MaxNumber, 
                request.SegmentSize);

            _logger.LogInformation(
                "Starting job for MaxNumber={MaxNumber:N0}, SegmentSize={SegmentSize:N0}. " +
                "Estimated memory: BasePrimes={BasePrimeMB}MB, Segment={SegmentMB}MB, " +
                "SqrtMaxNumber={SqrtMax:N0}",
                request.MaxNumber,
                request.SegmentSize,
                memoryEstimate.BasePrimeMemoryMB,
                memoryEstimate.SegmentMemoryMB,
                memoryEstimate.SqrtMaxNumber);

            var job = await _coordinator.StartJobAsync(2, request.MaxNumber, request.SegmentSize, HttpContext.RequestAborted);
            _logger.LogInformation("Started new sieve job {JobId} for range up to {MaxNumber:N0}", 
                job.JobId, job.MaxNumber);

            return Ok(job);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting job");
            return StatusCode(500, "Error starting job");
        }
    }

    /// <summary>
    /// Get current job status
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<JobStatusResponse>> GetStatus()
    {
        var status = await _coordinator.GetJobStatusAsync(HttpContext.RequestAborted);

        if (status == null)
        {
            return NotFound("No job is currently running or completed");
        }

        return Ok(status);
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [AllowAnonymous]
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
