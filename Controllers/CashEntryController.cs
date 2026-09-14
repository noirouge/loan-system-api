using LoanSystemAPI.Data;
using LoanSystemAPI.DTOs;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoanSystemAPI.Controllers
{

    [ApiController]
    [Route("api/cash-entries")]
    public class CashEntryController : Controller
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<CashEntryController> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly CashService _cashService;

        public CashEntryController(AppDbContext dbContext, ILogger<CashEntryController> logger, ICurrentUserService currentUserService, CashService cashService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _currentUserService = currentUserService;
            _cashService = cashService;
        }
        [HttpPost("contribution")]
        public async Task<ActionResult<CashEntryContributionDTO>> PostContribution([FromBody] CashEntryContributionDTO cashEntryDTO)
        {
            if (cashEntryDTO.Amount <= 0 || cashEntryDTO.Amount != Math.Round(cashEntryDTO.Amount, 2))
                return BadRequest(new { message = "The contribution amount must be greater than zero and have at most 2 decimals" });

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
                        CreatedBy = _currentUserService.UserId,
                        CreatedDate = DateTime.UtcNow,
                        status = CashEntryStatus.APPLIED,
         
                        
                    };

                    await _dbContext.CashEntries.AddAsync(cashEntry);
                    await _dbContext.SaveChangesAsync();

                    return StatusCode(StatusCodes.Status201Created, new { id = cashEntry.Id });
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
            if (cashEntryDTO.Amount <= 0 || cashEntryDTO.Amount != Math.Round(cashEntryDTO.Amount, 2))
                return BadRequest(new { message = "The amount to withdraw must be greater than zero and have at most 2 decimals" });

                try
                {
                    // THE LOCK MAKES A SECOND WITHDRAWAL OR EXPENSE WAIT UNTIL THIS ONE COMMITS
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    await _cashService.LockCashAsync();

                    var availableCash = await _cashService.GetAvailableCashAsync();
                    if (availableCash < cashEntryDTO.Amount)
                        return BadRequest(new { message = $"Not enough cash available to withdraw. Available cash: {availableCash}" });

                    var cashEntry = new CashEntry
                    {
                        Id = Guid.NewGuid(),
                        amount = -cashEntryDTO.Amount,
                        CounterpartyUserId = cashEntryDTO.CounterpartyUserId,
                        EntryType = CashEntryType.WITHDRAWAL,
                        Note = cashEntryDTO.Note,
                        ValueDate = cashEntryDTO.ValueDate,
                        CreatedBy = _currentUserService.UserId,
                        CreatedDate = DateTime.UtcNow,
                        status = CashEntryStatus.APPLIED,
                    };

                    await _dbContext.CashEntries.AddAsync(cashEntry);
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return StatusCode(StatusCodes.Status201Created, new { id = cashEntry.Id });
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
            if (cashEntryDTO.Amount <= 0 || cashEntryDTO.Amount != Math.Round(cashEntryDTO.Amount, 2))
                return BadRequest(new { message = "The expense amount must be greater than zero and have at most 2 decimals" });

            var counterpartyUserId = cashEntryDTO.CounterpartyUserId == Guid.Empty ? null : cashEntryDTO.CounterpartyUserId;

            if (counterpartyUserId == null && string.IsNullOrWhiteSpace(cashEntryDTO.Counterparty))
                return BadRequest(new { message = "One of the counterparty fields must be filled" });

                try
                {
                    // THE LOCK MAKES A SECOND WITHDRAWAL OR EXPENSE WAIT UNTIL THIS ONE COMMITS
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    await _cashService.LockCashAsync();

                    var availableCash = await _cashService.GetAvailableCashAsync();
                    if (availableCash < cashEntryDTO.Amount)
                        return BadRequest(new { message = $"Not enough cash available for this expense. Available cash: {availableCash}" });

                    var cashEntry = new CashEntry
                    {
                        Id = Guid.NewGuid(),
                        amount = -cashEntryDTO.Amount,
                        CounterpartyUserId = counterpartyUserId,
                        Counterparty = cashEntryDTO.Counterparty,
                        EntryType = CashEntryType.EXPENSE,
                        Note = cashEntryDTO.Note,
                        ValueDate = cashEntryDTO.ValueDate,
                        CreatedBy = _currentUserService.UserId,
                        CreatedDate = DateTime.UtcNow,
                        status = CashEntryStatus.APPLIED,
                    };

                    await _dbContext.CashEntries.AddAsync(cashEntry);
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return StatusCode(StatusCodes.Status201Created, new { id = cashEntry.Id });
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

                    // REVERSING A CONTRIBUTION TAKES ITS MONEY OUT OF THE CASH: IT NEEDS THE SAME CHECK AS A WITHDRAWAL (D-070)
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    await _cashService.LockCashAsync();
                    // UNDER THE LOCK A SECOND REVERSAL SEES THE FIRST ONE ALREADY SAVED
                    await _dbContext.Entry(cashEntry).ReloadAsync();
                    if (cashEntry.status == CashEntryStatus.REVERSED)
                        return Conflict(new { message = $"The cash entry with id {id} was already reversed" });
                    var availableCash = await _cashService.GetAvailableCashAsync();
                    if (cashEntry.amount > 0 && availableCash < cashEntry.amount)
                        return BadRequest(new { message = $"Not enough cash available to reverse this contribution. Available cash: {availableCash}" });

                    var reversalEntry = new CashEntry
                    {
                        Id = Guid.NewGuid(),
                        amount = -cashEntry.amount,
                        ReversesEntryId = cashEntry.Id,
                        EntryType = CashEntryType.REVERSAL,
                        Note = $"REVERSAL OF THE CASH ENTRY {cashEntry.Id}",
                        ValueDate = cashEntry.ValueDate,
                        CreatedBy = _currentUserService.UserId,
                        CreatedDate = DateTime.UtcNow,
                        status = CashEntryStatus.APPLIED,
                    };

                    cashEntry.status = CashEntryStatus.REVERSED;

                    await _dbContext.CashEntries.AddAsync(reversalEntry);
                    await _dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();

                    return StatusCode(StatusCodes.Status201Created, new { id = reversalEntry.Id });
                }
                // THE UNIQUE INDEX ON reverses_entry_id IS WHAT STOPS A DOUBLE REVERSAL, EVEN WITH TWO REQUESTS AT THE SAME TIME
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_cash_entries_reverses" })
                {
                    return Conflict(new { message = $"The cash entry with id {id} was already reversed" });
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
