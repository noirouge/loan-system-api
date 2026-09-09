using LoanSystemAPI.Data;
using LoanSystemAPI.DTOs;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using Microsoft.AspNetCore.Mvc;

namespace LoanSystemAPI.Controllers
{

    [ApiController]
    [Route("api/cash-entry")]
    public class CashEntryController : Controller
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<CashEntryController> _logger;
        private readonly IConfiguration _configuration;
        private readonly Guid _adminId;

        public CashEntryController(AppDbContext dbContext, ILogger<CashEntryController> logger, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;
            var configAdminId = _configuration["AdminId"] ?? throw new InvalidOperationException("NOT FOUND AdminId");
            if (Guid.TryParse(configAdminId, out Guid adminId))
            {
                _adminId = adminId;
            }
        }
        [HttpPost("contribution")]
        public async Task<ActionResult<CashEntryContributionDTO>> PostContribution([FromBody] CashEntryContributionDTO cashEntryDTO)
        {
                try
                {
                    var cashEntry = new CashEntry
                    {
                        Id = Guid.NewGuid(),
                        amount = cashEntryDTO.Amount,
                        CounterpartyUserId = cashEntryDTO.CounterpartyUserId,
                        EntryType = CashEntryType.CONTRIBUTION,
                        Note = cashEntryDTO.Note,
                        ValueDate = cashEntryDTO.ValueDate,
                        CreatedBy = _adminId,
                        CreatedDate = DateTime.UtcNow,
                        status = CashEntryStatus.APPLIED,
         
                        
                    };

                    await _dbContext.CashEntries.AddAsync(cashEntry);
                    await _dbContext.SaveChangesAsync();

                    return Created();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CASH ENTRY CONTRIBUTION ERROR");
                    return StatusCode(500, new { message = "ERROR COULD NOT SAVED THE CASH ENTRY CONTRIBUTION" });
                }
        }

        

    }
}
