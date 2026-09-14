using LoanSystemAPI.Data;
using LoanSystemAPI.Entities;
using LoanSystemAPI.DTOs;
using LoanSystemAPI.Enums;
using Microsoft.EntityFrameworkCore;

namespace LoanSystemAPI.Services
{
    // THE REPORTS ARE PROJECTIONS OF THE TWO LEDGERS. A REVERSAL COUNTS WITH THE TYPE OF THE ENTRY IT REVERSES, SO A PAYMENT AND
    // ITS REVERSAL ADD UP TO 0 (D-055). THE DATES FILTER BY value_date, AND A REVERSAL HAS THE value_date OF ITS ORIGINAL (D-066)
    public class ReportService
    {
        private readonly AppDbContext _dbContext;

        public ReportService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ReportCashBalanceDTO> GetCashBalanceAsync(DateOnly? date)
        {
            var sums = await EffectiveCashEntries()
                .Where(c => date == null || c.ValueDate <= date)
                .GroupBy(c => c.EntryType)
                .Select(g => new { EntryType = g.Key, Amount = g.Sum(c => c.Amount) })
                .ToDictionaryAsync(s => s.EntryType, s => s.Amount);

            return new ReportCashBalanceDTO
            {
                Date = date,
                Contributions = sums.GetValueOrDefault(CashEntryType.CONTRIBUTION),
                Withdrawals = sums.GetValueOrDefault(CashEntryType.WITHDRAWAL),
                Disbursements = sums.GetValueOrDefault(CashEntryType.DISBURSEMENT),
                Payments = sums.GetValueOrDefault(CashEntryType.PAYMENT),
                Expenses = sums.GetValueOrDefault(CashEntryType.EXPENSE),
                Balance = sums.Values.Sum(),
            };
        }

        // EVERY CASH ENTRY WITH THE TYPE THAT COUNTS IN THE REPORTS: ITS OWN, OR THE ONE OF THE ENTRY IT REVERSES
        private IQueryable<EffectiveCashEntry> EffectiveCashEntries()
        {
            return from c in _dbContext.CashEntries
                   join o in _dbContext.CashEntries on c.ReversesEntryId equals (Guid?)o.Id into originals
                   from o in originals.DefaultIfEmpty()
                   select new EffectiveCashEntry
                   {
                       EntryType = o == null ? c.EntryType : o.EntryType,
                       ValueDate = c.ValueDate,
                       Amount = c.amount,
                   };
        }

        private class EffectiveCashEntry
        {
            public CashEntryType EntryType { get; set; }
            public DateOnly ValueDate { get; set; }
            public decimal Amount { get; set; }
        }

        public async Task<decimal> SumInterestAsync(LoanEntryType entryType, DateOnly? from, DateOnly? to)
        {
            return await EffectiveLoanEntries()
                .Where(e => e.EntryType == entryType)
                .Where(e => from == null || e.ValueDate >= from)
                .Where(e => to == null || e.ValueDate <= to)
                .SumAsync(e => e.Interest);
        }

        // EVERY LOAN ENTRY WITH THE TYPE THAT COUNTS IN THE REPORTS: ITS OWN, OR THE ONE OF THE ENTRY IT REVERSES
        private IQueryable<EffectiveLoanEntry> EffectiveLoanEntries()
        {
            return from e in _dbContext.LoanEntries
                   join o in _dbContext.LoanEntries on e.ReversesEntryId equals (Guid?)o.Id into originals
                   from o in originals.DefaultIfEmpty()
                   select new EffectiveLoanEntry
                   {
                       EntryType = o == null ? e.EntryType : o.EntryType,
                       ValueDate = e.ValueDate,
                       Principal = e.Principal,
                       Interest = e.Interest,
                   };
        }

        private class EffectiveLoanEntry
        {
            public LoanEntryType EntryType { get; set; }
            public DateOnly ValueDate { get; set; }
            public decimal Principal { get; set; }
            public decimal Interest { get; set; }
        }

        // THE BALANCE IS THE SUM OF THE WHOLE COLUMNS, WITHOUT FILTERING TYPES. THE DELETED LOANS ARE HIDDEN BY THE QUERY FILTER OF Loan.
        // THE STATUS IS THE CURRENT ONE OF THE LOAN, ALSO WHEN date LOOKS AT THE PAST (D-067)
        public async Task<ReportPendingDTO> GetPendingAsync(DateOnly? date)
        {
            var sums = await (from e in _dbContext.LoanEntries
                              join l in _dbContext.Loans on e.LoanId equals l.Id
                              where date == null || e.ValueDate <= date
                              group e by l.Status == LoanStatus.WRITTENOFF into g
                              select new { WrittenOff = g.Key, Principal = g.Sum(e => e.Principal), Interest = g.Sum(e => e.Interest) })
                             .ToListAsync();

            var pending = sums.FirstOrDefault(s => !s.WrittenOff);
            var writtenOff = sums.FirstOrDefault(s => s.WrittenOff);

            return new ReportPendingDTO
            {
                Date = date,
                Principal = pending?.Principal ?? 0,
                Interest = pending?.Interest ?? 0,
                WrittenOffPrincipal = writtenOff?.Principal ?? 0,
                WrittenOffInterest = writtenOff?.Interest ?? 0,
            };
        }

        // THE EXPENSES ARE NEGATIVE IN THE CASH; THE REPORT RETURNS THEM AS A POSITIVE AMOUNT
        public async Task<decimal> SumExpensesAsync(DateOnly? from, DateOnly? to)
        {
            var expenses = await EffectiveCashEntries()
                .Where(c => c.EntryType == CashEntryType.EXPENSE)
                .Where(c => from == null || c.ValueDate >= from)
                .Where(c => to == null || c.ValueDate <= to)
                .SumAsync(c => c.Amount);

            return -expenses;
        }

        // THE COLLECTION SHEET OF A MONTH: EVERY LOAN THAT OWED SOMETHING BEFORE THE CUT OF DAY 1, SO IT APPEARS FROM THE MONTH AFTER ITS DATE
        // AND NOT AFTER THE MONTH IT WAS PAID OFF. THE MONTHLY FEE IS ALL THE PENDING INTEREST PLUS ORIGINAL / TERM, OR ALL THE PRINCIPAL WITHOUT TERM (D-075)
        public async Task<ReportMonthlyCollectionDTO> GetMonthlyCollectionAsync(DateOnly period, int? paymentDay)
        {
            var nextPeriod = period.AddMonths(1);

            // IgnoreQueryFilters TURNS OFF EVERY FILTER OF THE QUERY, SO THE DELETED LOANS ARE HIDDEN BY HAND
            var loans = await (from l in _dbContext.Loans
                               join c in _dbContext.Customers.IgnoreQueryFilters() on l.CustomerId equals c.Id
                               where l.Status != LoanStatus.DELETED && l.LoanDate < period && (paymentDay == null || l.PaymentDay == paymentDay)
                               select new { Loan = l, c.Fullname, c.Code })
                              .ToListAsync();
            var loanIds = loans.Select(l => l.Loan.Id).ToList();

            var entries = await _dbContext.LoanEntries.AsNoTracking()
                .Where(e => loanIds.Contains(e.LoanId) && e.ValueDate < nextPeriod)
                .ToListAsync();
            var frozenLoanIds = await _dbContext.Freezes
                .Where(f => loanIds.Contains(f.LoanId) && f.StartDate <= period && (f.EndDate == null || f.EndDate >= period))
                .Select(f => f.LoanId)
                .ToListAsync();

            // A REVERSAL COUNTS WITH THE TYPE AND THE PERIOD OF THE ENTRY IT REVERSES (D-055)
            var entriesById = entries.ToDictionary(e => e.Id);
            LoanEntry Effective(LoanEntry e) => e.ReversesEntryId != null && entriesById.TryGetValue(e.ReversesEntryId.Value, out var original) ? original : e;

            var report = new ReportMonthlyCollectionDTO { Month = period.ToString("yyyy-MM"), PaymentDay = paymentDay };
            foreach (var item in loans.OrderBy(l => l.Loan.PaymentDay).ThenBy(l => l.Fullname))
            {
                var loan = item.Loan;
                var loanEntries = entries.Where(e => e.LoanId == loan.Id).ToList();
                var owedPrincipal = loanEntries.Where(e => e.ValueDate < period).Sum(e => e.Principal);
                var owedInterest = loanEntries.Where(e => e.ValueDate < period).Sum(e => e.Interest);
                if (owedPrincipal + owedInterest <= 0)
                    continue;

                var monthCharge = loanEntries.Where(e => Effective(e).EntryType == LoanEntryType.INTERESTCHARGE && Effective(e).Period == period).Sum(e => e.Interest);
                var monthPayments = loanEntries.Where(e => Effective(e).EntryType == LoanEntryType.PAYMENT && e.ValueDate >= period).ToList();
                var interestDue = Math.Max(owedInterest, 0) + monthCharge;
                var principalDue = Math.Max(loan.Term == null ? owedPrincipal : Math.Min(MoneyRounding.Round(loan.Principal / loan.Term.Value), owedPrincipal), 0);
                var chargesUntilThisMonth = loanEntries.Count(e => e.EntryType == LoanEntryType.INTERESTCHARGE && e.Period <= period && e.Status == LoanEntryStatus.APPLIED);

                report.Loans.Add(new ReportMonthlyCollectionLoanDTO
                {
                    LoanId = loan.Id,
                    CustomerId = loan.CustomerId,
                    CustomerName = item.Fullname,
                    CustomerCode = item.Code,
                    LoanDate = loan.LoanDate,
                    PaymentDay = loan.PaymentDay,
                    Installment = loan.Term == null ? "0-1" : $"{chargesUntilThisMonth}-{loan.Term}",
                    OriginalPrincipal = loan.Principal,
                    InterestRate = loan.InterestRate,
                    OwedAtCut = owedPrincipal + owedInterest,
                    InterestDue = interestDue,
                    PrincipalDue = principalDue,
                    TotalDue = interestDue + principalDue,
                    Paid = -monthPayments.Sum(e => e.Principal + e.Interest),
                    PaidInterest = -monthPayments.Sum(e => e.Interest),
                    PaidPrincipal = -monthPayments.Sum(e => e.Principal),
                    Remaining = loanEntries.Sum(e => e.Principal + e.Interest),
                    Status = loan.Status,
                    Frozen = frozenLoanIds.Contains(loan.Id),
                });
            }

            report.TotalInterestDue = report.Loans.Sum(l => l.InterestDue);
            report.TotalPrincipalDue = report.Loans.Sum(l => l.PrincipalDue);
            report.TotalDue = report.Loans.Sum(l => l.TotalDue);
            report.TotalPaid = report.Loans.Sum(l => l.Paid);
            report.TotalRemaining = report.Loans.Sum(l => l.Remaining);

            return report;
        }
    }
}
