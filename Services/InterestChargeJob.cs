using LoanSystemAPI.Data;
using LoanSystemAPI.DTOs;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoanSystemAPI.Services
{
    // THE MONTHLY CUT: ON DAY 1 EVERY ACTIVE LOAN THAT IS NOT FROZEN RECEIVES ITS INTEREST CHARGE. THE FIRST ONE IS ON DAY 1 OF THE MONTH
    // AFTER THE LOAN DATE (D-068). THE CHARGE ONLY LOOKS AT THE ENTRIES BEFORE THE CUT, SO A PAST PERIOD GIVES THE SAME AMOUNT (D-074)
    public class InterestChargeJob : IDailyJob
    {
        public const string JobName = "interest-charges";

        private readonly AppDbContext _dbContext;
        private readonly JobRunner _jobRunner;
        private readonly LocalDateService _localDateService;
        private readonly ILogger<InterestChargeJob> _logger;

        public InterestChargeJob(AppDbContext dbContext, JobRunner jobRunner, LocalDateService localDateService, ILogger<InterestChargeJob> logger)
        {
            _dbContext = dbContext;
            _jobRunner = jobRunner;
            _localDateService = localDateService;
            _logger = logger;
        }

        public static DateOnly PeriodOf(DateOnly date)
        {
            return new DateOnly(date.Year, date.Month, 1);
        }

        // THE FIRST RUN OF A PERIOD IS interest-charges WITH ITS period. job_runs ALLOWS ONE SUCCESS PER PERIOD, SO A PERIOD THAT ALREADY
        // SUCCEEDED RUNS AGAIN AS interest-charges:yyyy-MM WITHOUT period. THE UNIQUE INDEX OF THE CHARGES KEEPS IT FROM DUPLICATING (D-074)
        public async Task<JobRun?> RunPeriodAsync(DateOnly period, CancellationToken cancellationToken = default)
        {
            var succeeded = await HasSucceededAsync(period, cancellationToken);

            return succeeded
                ? await _jobRunner.RunAsync($"{JobName}:{period:yyyy-MM}", null, token => ChargePeriodAsync(period, token), cancellationToken)
                : await _jobRunner.RunAsync(JobName, period, token => ChargePeriodAsync(period, token), cancellationToken);
        }

        private Task<bool> HasSucceededAsync(DateOnly period, CancellationToken cancellationToken)
        {
            return _dbContext.JobRuns.AnyAsync(j => j.JobName == JobName && j.Period == period && j.Status == JobRunStatus.SUCCESS, cancellationToken);
        }

        // ACTIVE, OLDER THAN THE PERIOD AND WITHOUT A FREEZE COVERING THE DAY OF THE CUT
        private IQueryable<Loan> LoansToCharge(DateOnly period)
        {
            return _dbContext.Loans.Where(l => l.Status == LoanStatus.ACTIVE
                && l.LoanDate < period
                && !_dbContext.Freezes.Any(f => f.LoanId == l.Id && f.StartDate <= period && (f.EndDate == null || f.EndDate >= period)));
        }

        private async Task<JobRunCounters> ChargePeriodAsync(DateOnly period, CancellationToken cancellationToken)
        {
            var counters = new JobRunCounters();

            // THE JOB HAS NO LOGGED USER: THE CHARGES ARE CREATED IN THE NAME OF THE OLDEST ACTIVE ADMIN
            var createdBy = await _dbContext.Users
                .Where(u => u.Role == UserRole.ADMIN && u.Status == UserStatus.ACTIVE)
                .OrderBy(u => u.CreatedDate)
                .Select(u => u.Id)
                .FirstAsync(cancellationToken);
            var loanIds = await LoansToCharge(period).Select(l => l.Id).ToListAsync(cancellationToken);

            foreach (var loanId in loanIds)
            {
                try
                {
                    if (await ChargeLoanAsync(loanId, period, createdBy, cancellationToken))
                        counters.Processed++;
                    else
                        counters.Skipped++;
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_loan_entries_loan_id_and_period" })
                {
                    counters.Skipped++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    counters.Failed++;
                    _logger.LogError(ex, "INTEREST CHARGE ERROR FOR THE LOAN {LoanId} IN {Period}", loanId, period);
                }
                finally
                {
                    _dbContext.ChangeTracker.Clear();
                }
            }

            return counters;
        }

        // FALSE WHEN THE LOAN ALREADY HAD THE CHARGE, STOPPED BEING ACTIVE OR OWES NOTHING
        private async Task<bool> ChargeLoanAsync(Guid loanId, DateOnly period, Guid createdBy, CancellationToken cancellationToken)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // THE SAME ROW LOCK AS A PAYMENT: A PAYMENT AT THE SAME TIME WAITS FOR THE CHARGE
            await _dbContext.Database.ExecuteSqlAsync($"SELECT id FROM loans WHERE id = {loanId} FOR UPDATE", cancellationToken);

            if (await _dbContext.LoanEntries.AnyAsync(e => e.LoanId == loanId && e.EntryType == LoanEntryType.INTERESTCHARGE && e.Period == period, cancellationToken))
                return false;

            var loan = await _dbContext.Loans.AsNoTracking().FirstAsync(l => l.Id == loanId, cancellationToken);

            // ONLY WHAT HAPPENED BEFORE THE CUT COUNTS
            var balance = await _dbContext.LoanEntries
                .Where(e => e.LoanId == loanId && e.ValueDate < period)
                .GroupBy(e => e.LoanId)
                .Select(g => new LoanBalanceDTO { Principal = g.Sum(e => e.Principal), Interest = g.Sum(e => e.Interest) })
                .FirstOrDefaultAsync(cancellationToken) ?? new LoanBalanceDTO();

            var interest = InterestCharge.Calculate(loan.InterestRate, balance);
            if (loan.Status != LoanStatus.ACTIVE || interest <= 0)
                return false;

            await _dbContext.LoanEntries.AddAsync(new LoanEntry
            {
                Id = Guid.NewGuid(),
                LoanId = loanId,
                EntryType = LoanEntryType.INTERESTCHARGE,
                Principal = 0,
                Interest = interest,
                Period = period,
                ValueDate = period,
                Note = $"INTEREST CHARGE OF {period:yyyy-MM}",
                Status = LoanEntryStatus.APPLIED,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
            }, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return true;
        }

        public string Name => JobName;

        // WHEN THE API STARTS AND EVERY DAY: FROM THE FIRST PERIOD OF THE OLDEST ACTIVE LOAN TO THE CURRENT ONE, RUNS EVERY PERIOD THAT NEVER
        // SUCCEEDED OR THAT STILL HAS A LOAN WITHOUT ITS CHARGE (A LOAN REGISTERED WITH A PAST DATE). IF THE 1ST FAILED, THE NEXT DAY CATCHES UP (D-054)
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var oldestLoanDate = await _dbContext.Loans
                .Where(l => l.Status == LoanStatus.ACTIVE)
                .MinAsync(l => (DateOnly?)l.LoanDate, cancellationToken);
            if (oldestLoanDate == null)
                return;

            var currentPeriod = PeriodOf(_localDateService.Today());
            for (var period = PeriodOf(oldestLoanDate.Value).AddMonths(1); period <= currentPeriod; period = period.AddMonths(1))
            {
                var pendingLoans = await LoansToCharge(period)
                    .AnyAsync(l => !_dbContext.LoanEntries.Any(e => e.LoanId == l.Id && e.EntryType == LoanEntryType.INTERESTCHARGE && e.Period == period), cancellationToken);
                if (!pendingLoans && await HasSucceededAsync(period, cancellationToken))
                    continue;

                // ANOTHER INSTANCE IS ALREADY RUNNING THE CUT
                if (await RunPeriodAsync(period, cancellationToken) == null)
                    return;
            }
        }
    }
}
