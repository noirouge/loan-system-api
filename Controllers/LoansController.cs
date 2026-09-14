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

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<LoanDetailDTO>> GetLoan([FromRoute] Guid id)
        {
            try
            {
                var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == id);
                if (loan == null)
                    return NotFound(new { message = $"The loan with id {id} was not found" });

                var customer = await _dbContext.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == loan.CustomerId);
                var balance = await _loanBalanceService.GetBalanceAsync(loan.Id);

                var entries = await _dbContext.LoanEntries
                    .Where(e => e.LoanId == id)
                    .OrderBy(e => e.ValueDate)
                    .ThenBy(e => e.CreatedDate)
                    .Select(e => new LoanEntryDTO
                    {
                        Id = e.Id,
                        EntryType = e.EntryType,
                        Principal = e.Principal,
                        Interest = e.Interest,
                        Period = e.Period,
                        ValueDate = e.ValueDate,
                        Status = e.Status,
                        ReversesEntryId = e.ReversesEntryId,
                        Note = e.Note,
                        CreatedDate = e.CreatedDate,
                    })
                    .ToListAsync();

                return Ok(new LoanDetailDTO
                {
                    Id = loan.Id,
                    CustomerId = loan.CustomerId,
                    CustomerFullname = customer?.Fullname ?? "",
                    Principal = loan.Principal,
                    Term = loan.Term,
                    InterestRate = loan.InterestRate,
                    LoanDate = loan.LoanDate,
                    PaymentDay = loan.PaymentDay,
                    Status = loan.Status,
                    Balance = balance,
                    SuggestedPayment = SuggestedPayment.Calculate(loan, balance),
                    Entries = entries,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR FINDING LOAN");
                return StatusCode(500, new { message = "ERROR FINDING LOAN" });
            }
        }

        [HttpPost("{id:guid}/payments")]
        public async Task<IActionResult> PostPayment([FromRoute] Guid id, [FromBody] LoanPaymentDTO paymentDTO)
        {
            if (paymentDTO.Amount <= 0 || paymentDTO.Amount != Math.Round(paymentDTO.Amount, 2))
                return BadRequest(new { message = "The payment amount must be greater than zero and have at most 2 decimals" });

            if (paymentDTO.ValueDate > _localDateService.Today())
                return BadRequest(new { message = "The payment date cannot be in the future" });

            try
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                // LOCKS THE LOAN ROW UNTIL THE COMMIT: A SECOND PAYMENT TO THE SAME LOAN WAITS HERE AND THEN READS THE UPDATED BALANCE
                await _dbContext.Database.ExecuteSqlAsync($"SELECT id FROM loans WHERE id = {id} FOR UPDATE");

                var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == id);
                if (loan == null)
                    return NotFound(new { message = $"The loan with id {id} was not found" });

                if (paymentDTO.ValueDate < loan.LoanDate)
                    return BadRequest(new { message = "The payment date cannot be earlier than the loan date" });

                // A RETRY WITH THE SAME KEY GETS THE PAYMENT ALREADY SAVED. IT IS CHECKED UNDER THE LOAN LOCK AND BEFORE THE
                // AMOUNT CHECK, SO RETRYING A PAYMENT THAT SETTLED THE DEBT IS NOT REJECTED FOR BEING LARGER THAN THE DEBT
                var savedWithKey = await _dbContext.LoanEntries.AsNoTracking().FirstOrDefaultAsync(e => e.IdempotencyKey == paymentDTO.IdempotencyKey);
                if (savedWithKey != null)
                    return IdempotentPaymentResult(savedWithKey, loan.Id, paymentDTO);
                var balanceBeforePayment = await _loanBalanceService.GetBalanceAsync(loan.Id);
                if (paymentDTO.Amount > balanceBeforePayment.Total)
                    return BadRequest(new { message = $"The payment is greater than the total debt. Maximum: {balanceBeforePayment.Total}" });

                // CASCADE: FIRST ALL THE PENDING INTEREST, THEN PRINCIPAL
                var interestPaid = Math.Min(paymentDTO.Amount, Math.Max(balanceBeforePayment.Interest, 0));
                var principalPaid = paymentDTO.Amount - interestPaid;

                var payment = new LoanEntry
                {
                    Id = Guid.NewGuid(),
                    LoanId = loan.Id,
                    EntryType = LoanEntryType.PAYMENT,
                    IdempotencyKey = paymentDTO.IdempotencyKey,
                    Principal = -principalPaid,
                    Interest = -interestPaid,
                    ValueDate = paymentDTO.ValueDate,
                    Note = paymentDTO.Note,
                    Status = LoanEntryStatus.APPLIED,
                    CreatedBy = _currentUserService.UserId,
                    CreatedDate = DateTime.UtcNow,
                };
                await _dbContext.LoanEntries.AddAsync(payment);
                try
                {
                    await _dbContext.SaveChangesAsync();
                }
                // THE UNIQUE INDEX IS THE REAL GUARD: IT ALSO STOPS THE SAME KEY SENT TO TWO DIFFERENT LOANS AT THE SAME TIME
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_loan_entries_idempotency_key" })
                {
                    await transaction.RollbackAsync();
                    var savedPayment = await _dbContext.LoanEntries.AsNoTracking().FirstAsync(e => e.IdempotencyKey == paymentDTO.IdempotencyKey);
                    return IdempotentPaymentResult(savedPayment, loan.Id, paymentDTO);
                }

                // THE CASH RECEIVES THE WHOLE AMOUNT; THE SPLIT BETWEEN INTEREST AND PRINCIPAL LIVES IN THE LOAN ENTRY
                var cashEntry = new CashEntry
                {
                    Id = Guid.NewGuid(),
                    amount = paymentDTO.Amount,
                    EntryType = CashEntryType.PAYMENT,
                    LoanEntryId = payment.Id,
                    ValueDate = paymentDTO.ValueDate,
                    Note = $"PAYMENT OF THE LOAN {loan.Id}",
                    status = CashEntryStatus.APPLIED,
                    CreatedBy = _currentUserService.UserId,
                    CreatedDate = DateTime.UtcNow,
                };
                await _dbContext.CashEntries.AddAsync(cashEntry);
                await _dbContext.SaveChangesAsync();

                await UpdateLoanStatusAfterEntryAsync(loan);
                await transaction.CommitAsync();

                return StatusCode(StatusCodes.Status201Created, new { id = payment.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LOAN PAYMENT ERROR");
                return StatusCode(500, new { message = "ERROR COULD NOT SAVED THE LOAN PAYMENT" });
            }
        }

        // SAME KEY AND SAME PAYMENT: THE CLIENT RETRIED, SO IT GETS THE SAME ANSWER.
        // SAME KEY WITH A DIFFERENT PAYMENT, OR ON ANOTHER LOAN, IS A CLIENT ERROR
        private IActionResult IdempotentPaymentResult(LoanEntry savedPayment, Guid loanId, LoanPaymentDTO paymentDTO)
        {
            var isSamePayment = savedPayment.EntryType == LoanEntryType.PAYMENT
                && savedPayment.LoanId == loanId
                && -(savedPayment.Principal + savedPayment.Interest) == paymentDTO.Amount
                && savedPayment.ValueDate == paymentDTO.ValueDate
                && (savedPayment.Note ?? "") == (paymentDTO.Note ?? "");

            if (!isSamePayment)
                return UnprocessableEntity(new { message = "This idempotency key was already used for a different payment" });

            return StatusCode(StatusCodes.Status201Created, new { id = savedPayment.Id });
        }

        [HttpPost("{id:guid}/forgiveness")]
        public async Task<IActionResult> PostForgiveness([FromRoute] Guid id, [FromBody] LoanForgivenessDTO forgivenessDTO)
        {
            if (forgivenessDTO.Amount <= 0 || forgivenessDTO.Amount != Math.Round(forgivenessDTO.Amount, 2))
                return BadRequest(new { message = "The amount to forgive must be greater than zero and have at most 2 decimals" });

            if (forgivenessDTO.ValueDate > _localDateService.Today())
                return BadRequest(new { message = "The forgiveness date cannot be in the future" });

            try
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync();
                await _dbContext.Database.ExecuteSqlAsync($"SELECT id FROM loans WHERE id = {id} FOR UPDATE");

                var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == id);
                if (loan == null)
                    return NotFound(new { message = $"The loan with id {id} was not found" });

                if (forgivenessDTO.ValueDate < loan.LoanDate)
                    return BadRequest(new { message = "The forgiveness date cannot be earlier than the loan date" });

                // ONLY INTEREST IS FORGIVEN, AND NEVER MORE THAN WHAT IS PENDING: THE INTEREST CANNOT END NEGATIVE
                var balanceBeforeForgiveness = await _loanBalanceService.GetBalanceAsync(loan.Id);
                if (forgivenessDTO.Amount > balanceBeforeForgiveness.Interest)
                    return BadRequest(new { message = $"The amount to forgive is greater than the pending interest. Maximum: {balanceBeforeForgiveness.Interest}" });

                // NO CASH ENTRY: NO MONEY COMES IN OR GOES OUT
                var forgiveness = new LoanEntry
                {
                    Id = Guid.NewGuid(),
                    LoanId = loan.Id,
                    EntryType = LoanEntryType.FORGIVENESS,
                    Principal = 0,
                    Interest = -forgivenessDTO.Amount,
                    ValueDate = forgivenessDTO.ValueDate,
                    Note = forgivenessDTO.Note,
                    Status = LoanEntryStatus.APPLIED,
                    CreatedBy = _currentUserService.UserId,
                    CreatedDate = DateTime.UtcNow,
                };
                await _dbContext.LoanEntries.AddAsync(forgiveness);
                await _dbContext.SaveChangesAsync();

                await UpdateLoanStatusAfterEntryAsync(loan);
                await transaction.CommitAsync();

                return StatusCode(StatusCodes.Status201Created, new { id = forgiveness.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LOAN FORGIVENESS ERROR");
                return StatusCode(500, new { message = "ERROR COULD NOT SAVED THE LOAN FORGIVENESS" });
            }
        }

        [HttpPost("entries/{entryId:guid}/reversal")]
        public async Task<IActionResult> PostEntryReversal([FromRoute] Guid entryId)
        {
            try
            {
                var original = await _dbContext.LoanEntries.FirstOrDefaultAsync(e => e.Id == entryId);
                if (original == null)
                    return NotFound(new { message = $"The loan entry to reverse with id {entryId} was not found" });

                // INTEREST CHARGES ARE FORGIVEN, NOT REVERSED (D-017). A DISBURSEMENT IS UNDONE BY DELETING THE LOAN (D-056)
                if (original.EntryType != LoanEntryType.PAYMENT && original.EntryType != LoanEntryType.FORGIVENESS)
                    return BadRequest(new { message = "Only a payment or a forgiveness can be reversed from this endpoint" });

                await using var transaction = await _dbContext.Database.BeginTransactionAsync();
                await _dbContext.Database.ExecuteSqlAsync($"SELECT id FROM loans WHERE id = {original.LoanId} FOR UPDATE");

                var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == original.LoanId);
                if (loan == null)
                    return NotFound(new { message = $"The loan of the entry with id {entryId} was not found" });

                // SAME AMOUNTS WITH THE OPPOSITE SIGN, AND THE SAME DATE AS THE ORIGINAL (D-013)
                var reversal = new LoanEntry
                {
                    Id = Guid.NewGuid(),
                    LoanId = original.LoanId,
                    EntryType = LoanEntryType.REVERSAL,
                    Principal = -original.Principal,
                    Interest = -original.Interest,
                    ReversesEntryId = original.Id,
                    ValueDate = original.ValueDate,
                    Note = $"REVERSAL OF THE LOAN ENTRY {original.Id}",
                    Status = LoanEntryStatus.APPLIED,
                    CreatedBy = _currentUserService.UserId,
                    CreatedDate = DateTime.UtcNow,
                };
                original.Status = LoanEntryStatus.REVERSED;
                await _dbContext.LoanEntries.AddAsync(reversal);

                try
                {
                    await _dbContext.SaveChangesAsync();
                }
                // THE UNIQUE INDEX ON reverses_entry_id STOPS A DOUBLE REVERSAL, EVEN WITH TWO REQUESTS AT THE SAME TIME
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_loan_entries_reverses_entry_id" })
                {
                    return Conflict(new { message = $"The loan entry with id {entryId} was already reversed" });
                }

                // THE CASH ENTRY OF A PAYMENT IS REVERSED TOO, POINTING TO THE NEW REVERSAL ENTRY (D-018)
                var originalCash = await _dbContext.CashEntries.FirstOrDefaultAsync(c => c.LoanEntryId == original.Id);
                if (originalCash != null)
                {
                    var cashReversal = new CashEntry
                    {
                        Id = Guid.NewGuid(),
                        amount = -originalCash.amount,
                        EntryType = CashEntryType.REVERSAL,
                        LoanEntryId = reversal.Id,
                        ReversesEntryId = originalCash.Id,
                        ValueDate = originalCash.ValueDate,
                        Note = $"REVERSAL OF THE CASH ENTRY {originalCash.Id}",
                        status = CashEntryStatus.APPLIED,
                        CreatedBy = _currentUserService.UserId,
                        CreatedDate = DateTime.UtcNow,
                    };
                    originalCash.status = CashEntryStatus.REVERSED;
                    await _dbContext.CashEntries.AddAsync(cashReversal);
                    await _dbContext.SaveChangesAsync();
                }

                await UpdateLoanStatusAfterEntryAsync(loan);
                await transaction.CommitAsync();

                return StatusCode(StatusCodes.Status201Created, new { id = reversal.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LOAN ENTRY REVERSAL ERROR");
                return StatusCode(500, new { message = "ERROR COULD NOT SAVED THE LOAN ENTRY REVERSAL" });
            }
        }

        // A LOAN CLOSES BY ITSELF WHEN NOTHING IS OWED, AND OPENS AGAIN IF A REVERSAL GIVES IT BALANCE BACK (D-058)
        private async Task UpdateLoanStatusAfterEntryAsync(Loan loan)
        {
            var balance = await _loanBalanceService.GetBalanceAsync(loan.Id);

            if (balance.Total <= 0 && (loan.Status == LoanStatus.ACTIVE || loan.Status == LoanStatus.WRITTENOFF))
                loan.Status = LoanStatus.CLOSED;
            else if (balance.Total > 0 && loan.Status == LoanStatus.CLOSED)
                loan.Status = LoanStatus.ACTIVE;
            else
                return;

            loan.UpdatedBy = _currentUserService.UserId;
            loan.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        // THE CUSTOMER IS NOT GOING TO PAY: THE LOAN STOPS GENERATING INTEREST, BUT A PAYMENT IS STILL ACCEPTED IF IT COMES (D-057)
        [HttpPost("{id:guid}/write-off")]
        public async Task<IActionResult> PostWriteOff([FromRoute] Guid id)
        {
            try
            {
                var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == id);
                if (loan == null)
                    return NotFound(new { message = $"The loan with id {id} was not found" });

                if (loan.Status != LoanStatus.ACTIVE)
                    return BadRequest(new { message = "Only an active loan can be written off" });

                loan.Status = LoanStatus.WRITTENOFF;
                loan.UpdatedBy = _currentUserService.UserId;
                loan.UpdatedDate = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LOAN WRITE OFF ERROR");
                return StatusCode(500, new { message = "ERROR COULD NOT WRITE OFF THE LOAN" });
            }
        }
    }
}
