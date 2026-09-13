using LoanSystemAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanSystemAPI.Services
{
    public class CashService
    {
        // ANY NUMBER WORKS, IT ONLY HAS TO BE THE SAME FOR EVERY OPERATION THAT TAKES CASH OUT
        private const long CashLockKey = 1001;

        private readonly AppDbContext _dbContext;

        public CashService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // REVERSED ENTRIES ARE NOT FILTERED: THE REVERSAL CANCELS THEM BY SIGN
        public async Task<decimal> GetAvailableCashAsync()
        {
            return await _dbContext.CashEntries.SumAsync(c => c.amount);
        }

        // MUST RUN INSIDE A TRANSACTION: POSTGRES RELEASES THE LOCK ON COMMIT OR ROLLBACK.
        // IT MAKES TWO WITHDRAWALS AT THE SAME TIME WAIT FOR EACH OTHER INSTEAD OF BOTH SEEING THE SAME BALANCE
        public async Task LockCashAsync()
        {
            await _dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({CashLockKey})");
        }
    }
}
