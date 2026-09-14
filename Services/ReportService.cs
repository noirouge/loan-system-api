using LoanSystemAPI.Data;
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
    }
}
