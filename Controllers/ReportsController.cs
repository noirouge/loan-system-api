using LoanSystemAPI.DTOs;
using LoanSystemAPI.Enums;
using LoanSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace LoanSystemAPI.Controllers
{

    [ApiController]
    [Route("api/reports")]
    public class ReportsController : Controller
    {
        private readonly ILogger<ReportsController> _logger;
        private readonly ReportService _reportService;

        public ReportsController(ILogger<ReportsController> logger, ReportService reportService)
        {
            _logger = logger;
            _reportService = reportService;
        }

        // THE CASH UNTIL date (INCLUSIVE), OR ALL OF IT WITHOUT date
        [HttpGet("cash-balance")]
        public async Task<ActionResult<ReportCashBalanceDTO>> GetCashBalance([FromQuery] DateOnly? date)
        {
            try
            {
                return Ok(await _reportService.GetCashBalanceAsync(date));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CASH BALANCE REPORT ERROR");
                return StatusCode(500, new { message = "ERROR GENERATING THE CASH BALANCE REPORT" });
            }
        }

        // ALL THE INTEREST CHARGED TO THE CUSTOMERS, PAID OR NOT
        [HttpGet("accrued-interest")]
        public async Task<ActionResult<ReportAmountDTO>> GetAccruedInterest([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        {
            if (from > to)
                return BadRequest(new { message = "The start date cannot be later than the end date" });

            try
            {
                var accruedInterest = await _reportService.SumInterestAsync(LoanEntryType.INTERESTCHARGE, from, to);
                return Ok(new ReportAmountDTO { From = from, To = to, Amount = accruedInterest });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ACCRUED INTEREST REPORT ERROR");
                return StatusCode(500, new { message = "ERROR GENERATING THE ACCRUED INTEREST REPORT" });
            }
        }

        // THE INTEREST THAT CAME IN WITH THE PAYMENTS, AS A POSITIVE AMOUNT. IT IS THE INCOME: THE PRINCIPAL THAT COMES BACK IS NOT
        [HttpGet("collected-interest")]
        public async Task<ActionResult<ReportAmountDTO>> GetCollectedInterest([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        {
            if (from > to)
                return BadRequest(new { message = "The start date cannot be later than the end date" });

            try
            {
                var collectedInterest = -await _reportService.SumInterestAsync(LoanEntryType.PAYMENT, from, to);
                return Ok(new ReportAmountDTO { From = from, To = to, Amount = collectedInterest });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "COLLECTED INTEREST REPORT ERROR");
                return StatusCode(500, new { message = "ERROR GENERATING THE COLLECTED INTEREST REPORT" });
            }
        }
    }
}
