using LoanSystemAPI.DTOs;
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
    }
}
