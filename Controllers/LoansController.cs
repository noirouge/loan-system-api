using LoanSystemAPI.Data;
using LoanSystemAPI.DTOs;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoanSystemAPI.Controllers
{

    [ApiController]
    [Route("api/loans")]
    public class LoansController : Controller
    {
        private readonly ILogger<LoansController> _logger;
        private readonly AppDbContext _dbContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly CashService _cashService;
        private readonly LoanBalanceService _loanBalanceService;
        private readonly LocalDateService _localDateService;

        public LoansController(ILogger<LoansController> logger, AppDbContext dbContext, ICurrentUserService currentUserService, CashService cashService, LoanBalanceService loanBalanceService, LocalDateService localDateService)
        {
            _logger = logger;
            _dbContext = dbContext;
            _currentUserService = currentUserService;
            _cashService = cashService;
            _loanBalanceService = loanBalanceService;
            _localDateService = localDateService;
        }

        [HttpPost]
        public async Task<IActionResult> PostLoan([FromBody] LoanCreateDTO loanDTO)
        {
            if (loanDTO.Principal <= 0 || loanDTO.Principal != Math.Round(loanDTO.Principal, 2))
                return BadRequest(new { message = "The principal must be greater than zero and have at most 2 decimals" });

            if (loanDTO.InterestRate <= 0 || loanDTO.InterestRate > 1 || loanDTO.InterestRate != Math.Round(loanDTO.InterestRate, 4))
                return BadRequest(new { message = "The interest rate is a fraction greater than 0 and at most 1, with at most 4 decimals: 0.10 is 10%" });

            if (loanDTO.Term != null && loanDTO.Term <= 0)
                return BadRequest(new { message = "The term must be greater than zero when it is sent" });

            if (loanDTO.PaymentDay < 1 || loanDTO.PaymentDay > 28)
                return BadRequest(new { message = "The payment day must be between 1 and 28" });

            if (loanDTO.LoanDate > _localDateService.Today())
                return BadRequest(new { message = "The loan date cannot be in the future" });

            try
            {
                var customerExists = await _dbContext.Customers.AnyAsync(c => c.Id == loanDTO.CustomerId);
                if (!customerExists)
                    return NotFound(new { message = $"The customer with id {loanDTO.CustomerId} was not found" });

                // THE CASH LOCK MAKES A WITHDRAWAL, AN EXPENSE OR ANOTHER LOAN WAIT UNTIL THIS ONE COMMITS
                await using var transaction = await _dbContext.Database.BeginTransactionAsync();
                await _cashService.LockCashAsync();

                var availableCash = await _cashService.GetAvailableCashAsync();
                if (availableCash < loanDTO.Principal)
                    return BadRequest(new { message = $"Not enough cash available to disburse this loan. Available cash: {availableCash}" });

                var loan = new Loan
                {
                    Id = Guid.NewGuid(),
                    CustomerId = loanDTO.CustomerId,
                    Principal = loanDTO.Principal,
                    Term = loanDTO.Term,
                    InterestRate = loanDTO.InterestRate,
                    LoanDate = loanDTO.LoanDate,
                    PaymentDay = loanDTO.PaymentDay,
                    Status = LoanStatus.ACTIVE,
                    CreatedBy = _currentUserService.UserId,
                    CreatedDate = DateTime.UtcNow,
                };

                // SAVED ONE BY ONE: THE ENTITIES HAVE NO NAVIGATIONS, SO EF DOES NOT KNOW THE ORDER THE FOREIGN KEYS NEED
                await _dbContext.Loans.AddAsync(loan);
                await _dbContext.SaveChangesAsync();

                var disbursement = new LoanEntry
                {
                    Id = Guid.NewGuid(),
                    LoanId = loan.Id,
                    EntryType = LoanEntryType.DISBURSEMENT,
                    Principal = loanDTO.Principal,
                    Interest = 0,
                    ValueDate = loanDTO.LoanDate,
                    Note = $"DISBURSEMENT OF THE LOAN {loan.Id}",
                    Status = LoanEntryStatus.APPLIED,
                    CreatedBy = _currentUserService.UserId,
                    CreatedDate = DateTime.UtcNow,
                };
                await _dbContext.LoanEntries.AddAsync(disbursement);
                await _dbContext.SaveChangesAsync();

                var cashEntry = new CashEntry
                {
                    Id = Guid.NewGuid(),
                    amount = -loanDTO.Principal,
                    EntryType = CashEntryType.DISBURSEMENT,
                    LoanEntryId = disbursement.Id,
                    ValueDate = loanDTO.LoanDate,
                    Note = $"DISBURSEMENT OF THE LOAN {loan.Id}",
                    status = CashEntryStatus.APPLIED,
                    CreatedBy = _currentUserService.UserId,
                    CreatedDate = DateTime.UtcNow,
                };
                await _dbContext.CashEntries.AddAsync(cashEntry);
                await _dbContext.SaveChangesAsync();

                await transaction.CommitAsync();

                return StatusCode(StatusCodes.Status201Created, new { id = loan.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LOAN CREATION ERROR");
                return StatusCode(500, new { message = "ERROR COULD NOT SAVED THE LOAN" });
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<LoanDTO>>> GetLoans()
        {
            try
            {
                // THE CUSTOMER IS SHOWN EVEN IF IT WAS DELETED LATER: ITS LOAN STILL EXISTS
                var loans = await (from l in _dbContext.Loans
                                   join c in _dbContext.Customers.IgnoreQueryFilters() on l.CustomerId equals c.Id
                                   orderby l.LoanDate descending, l.CreatedDate descending
                                   select new LoanDTO
                                   {
                                       Id = l.Id,
                                       CustomerId = l.CustomerId,
                                       CustomerFullname = c.Fullname,
                                       Principal = l.Principal,
                                       Term = l.Term,
                                       InterestRate = l.InterestRate,
                                       LoanDate = l.LoanDate,
                                       PaymentDay = l.PaymentDay,
                                       Status = l.Status,
                                   })
                                  .ToListAsync();

                var balances = await _loanBalanceService.GetBalancesAsync(loans.Select(l => l.Id));
                foreach (var loan in loans)
                    loan.Balance = balances[loan.Id];

                return Ok(loans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONSULTING LOANS");
                return StatusCode(500, new { message = "Error Consulting Loans" });
            }
        }
    }
}
