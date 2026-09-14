using LoanSystemAPI.Data;
using LoanSystemAPI.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LoanSystemAPI.Services
{
    public class LoanBalanceService
    {
        private readonly AppDbContext _dbContext;

        public LoanBalanceService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<LoanBalanceDTO> GetBalanceAsync(Guid loanId)
        {
            var balances = await GetBalancesAsync(new[] { loanId });
            return balances[loanId];
        }

        // ONE QUERY FOR ALL THE LOANS. REVERSED ENTRIES ARE NOT FILTERED: THE REVERSAL CANCELS THEM BY SIGN
        public async Task<Dictionary<Guid, LoanBalanceDTO>> GetBalancesAsync(IEnumerable<Guid> loanIds)
        {
            var ids = loanIds.Distinct().ToList();

            var sums = await _dbContext.LoanEntries
                .Where(e => ids.Contains(e.LoanId))
                .GroupBy(e => e.LoanId)
                .Select(g => new { LoanId = g.Key, Principal = g.Sum(e => e.Principal), Interest = g.Sum(e => e.Interest) })
                .ToListAsync();

            return ids.ToDictionary(
                id => id,
                id => sums
                    .Where(s => s.LoanId == id)
                    .Select(s => new LoanBalanceDTO { Principal = s.Principal, Interest = s.Interest })
                    .FirstOrDefault() ?? new LoanBalanceDTO());
        }
    }
}
