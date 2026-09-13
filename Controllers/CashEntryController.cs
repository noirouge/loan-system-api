using LoanSystemAPI.Data;
using LoanSystemAPI.DTOs;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoanSystemAPI.Controllers
{

    [ApiController]
    [Route("api/cash-entries")]
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

        [HttpPost("withdrawal")]
        public async Task<ActionResult<CashEntryWithdrawalDTO>> PostWithdrawal([FromBody] CashEntryWithdrawalDTO cashEntryDTO)
        {
            if (cashEntryDTO.Amount <= 0)
                return BadRequest(new { message = "The amount to withdraw must be greater than zero" });

                try
                {
                    var cashEntry = new CashEntry
                    {
                        Id = Guid.NewGuid(),
                        amount = -cashEntryDTO.Amount,
                        CounterpartyUserId = cashEntryDTO.CounterpartyUserId,
                        EntryType = CashEntryType.WITHDRAWAL,
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
                    _logger.LogError(ex, "CASH ENTRY WITHDRAWAL ERROR");
                    return StatusCode(500, new { message = "ERROR COULD NOT SAVED THE CASH ENTRY WITHDRAWAL" });
                }
        }

        [HttpPost("expense")]
        public async Task<ActionResult<CashEntryExpenseDTO>> PostExpense([FromBody] CashEntryExpenseDTO cashEntryDTO)
        {
            if (cashEntryDTO.Amount <= 0)
                return BadRequest(new { message = "The expense amount must be greater than zero" });

            var counterpartyUserId = cashEntryDTO.CounterpartyUserId == Guid.Empty ? null : cashEntryDTO.CounterpartyUserId;

            if (counterpartyUserId == null && string.IsNullOrWhiteSpace(cashEntryDTO.Counterparty))
                return BadRequest(new { message = "One of the counterparty fields must be filled" });

                try
                {
                    var cashEntry = new CashEntry
                    {
                        Id = Guid.NewGuid(),
                        amount = -cashEntryDTO.Amount,
                        CounterpartyUserId = counterpartyUserId,
                        Counterparty = cashEntryDTO.Counterparty,
                        EntryType = CashEntryType.EXPENSE,
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
                    _logger.LogError(ex, "CASH ENTRY EXPENSE ERROR");
                    return StatusCode(500, new { message = "ERROR COULD NOT SAVED THE CASH ENTRY EXPENSE" });
                }
        }

        [HttpPost("reversal/{id:guid}")]
        public async Task<IActionResult> PostReversal([FromRoute] Guid id)
        {
                try
                {
                    var cashEntry = await _dbContext.CashEntries.FirstOrDefaultAsync(c => c.Id == id);

                    if (cashEntry == null)
                        return NotFound(new { message = $"The cash entry to reverse with id {id} was not found" });

                    if (cashEntry.EntryType != CashEntryType.CONTRIBUTION && cashEntry.EntryType != CashEntryType.WITHDRAWAL && cashEntry.EntryType != CashEntryType.EXPENSE)
                        return BadRequest(new { message = "Only a contribution, a withdrawal or an expense can be reversed from this endpoint" });

                    if (cashEntry.status == CashEntryStatus.REVERSED)
                        return BadRequest(new { message = $"The cash entry with id {id} was already reversed" });

                    var reversalEntry = new CashEntry
                    {
                        Id = Guid.NewGuid(),
                        amount = -cashEntry.amount,
                        ReversesEntryId = cashEntry.Id,
                        EntryType = CashEntryType.REVERSAL,
                        Note = $"REVERSAL OF THE CASH ENTRY {cashEntry.Id}",
                        ValueDate = DateOnly.FromDateTime(DateTime.UtcNow),
                        CreatedBy = _adminId,
                        CreatedDate = DateTime.UtcNow,
                        status = CashEntryStatus.APPLIED,
                    };

                    cashEntry.status = CashEntryStatus.REVERSED;
                    cashEntry.UpdatedBy = _adminId;
                    cashEntry.UpdatedDate = DateTime.UtcNow;

                    await _dbContext.CashEntries.AddAsync(reversalEntry);
                    await _dbContext.SaveChangesAsync();

                    return Created();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CASH ENTRY REVERSAL ERROR");
                    return StatusCode(500, new { message = "ERROR COULD NOT SAVED THE CASH ENTRY REVERSAL" });
                }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CashEntryDTO>>> GetCashEntries()
        {
            try
            {
                var cashEntries = await _dbContext.CashEntries
                    .OrderByDescending(c => c.ValueDate)
                    .ThenByDescending(c => c.CreatedDate)
                    .Select(c => new CashEntryDTO
                    {
                        Id = c.Id,
                        EntryType = c.EntryType,
                        Amount = c.amount,
                        ValueDate = c.ValueDate,
                        Note = c.Note,
                        Status = c.status,
                        CounterpartyUserId = c.CounterpartyUserId,
                        Counterparty = c.Counterparty,
                        LoanEntryId = c.LoanEntryId,
                        ReversesEntryId = c.ReversesEntryId,
                        CreatedDate = c.CreatedDate,
                    })
                    .ToListAsync();

                return Ok(cashEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONSULTING CASH ENTRIES");
                return StatusCode(500, new { message = "Error Consulting Cash Entries" });
            }
        }

        

    }
}
