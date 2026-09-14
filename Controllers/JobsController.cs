using LoanSystemAPI.DTOs;
using LoanSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace LoanSystemAPI.Controllers
{

    [ApiController]
    [Route("api/jobs")]
    public class JobsController : Controller
    {
        private readonly ILogger<JobsController> _logger;
        private readonly InterestChargeJob _interestChargeJob;
        private readonly LocalDateService _localDateService;

        public JobsController(ILogger<JobsController> logger, InterestChargeJob interestChargeJob, LocalDateService localDateService)
        {
            _logger = logger;
            _interestChargeJob = interestChargeJob;
            _localDateService = localDateService;
        }

        // RUNS THE CUT OF ONE PERIOD BY HAND. RUNNING IT AGAIN NEVER DUPLICATES A CHARGE
        [HttpPost("interest-charges/{period}")]
        public async Task<ActionResult<JobRunDTO>> PostInterestCharges([FromRoute] DateOnly period)
        {
            if (period.Day != 1 || period > InterestChargeJob.PeriodOf(_localDateService.Today()))
                return BadRequest(new { message = "The period must be day 1 of a month that already started" });

            try
            {
                var jobRun = await _interestChargeJob.RunPeriodAsync(period);
                if (jobRun == null)
                    return Conflict(new { message = "The interest charge job is already running" });

                return Ok(new JobRunDTO
                {
                    Id = jobRun.Id,
                    JobName = jobRun.JobName,
                    Period = jobRun.Period,
                    Status = jobRun.Status,
                    Processed = jobRun.Processed,
                    Skipped = jobRun.Skipped,
                    Failed = jobRun.Failed,
                    ErrorMessage = jobRun.ErrorMessage,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "INTEREST CHARGE JOB ERROR");
                return StatusCode(500, new { message = "ERROR RUNNING THE INTEREST CHARGE JOB" });
            }
        }
    }
}
