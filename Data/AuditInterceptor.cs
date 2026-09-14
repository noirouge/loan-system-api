using LoanSystemAPI.Entities;
using LoanSystemAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace LoanSystemAPI.Data
{
    // AUDITS EVERY CHANGE SAVED THROUGH EF, WITHOUT CALLS FROM THE CONTROLLERS THAT SOMEONE WOULD FORGET (D-053). THE LOGS ARE BUILT
    // BEFORE SAVING, WHILE THE ChangeTracker STILL HAS THE ORIGINAL VALUES, AND THEY ARE WRITTEN ONLY AFTER THE CHANGE IS COMMITTED (D-030).
    // BULK OPERATIONS (ExecuteUpdate, ExecuteDelete) DO NOT GO THROUGH THE ChangeTracker AND ARE NOT AUDITED
    public class AuditInterceptor : SaveChangesInterceptor, IDbTransactionInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AuditLogWriter _auditLogWriter;

        // BUILT IN SavingChanges, WAITING FOR SaveChanges TO SUCCEED
        private List<AuditLog> _saving = new();
        // ALREADY SAVED INSIDE AN EXPLICIT TRANSACTION, WAITING FOR ITS COMMIT
        private List<AuditLog> _waitingCommit = new();

        public AuditInterceptor(IHttpContextAccessor httpContextAccessor, AuditLogWriter auditLogWriter)
        {
            _httpContextAccessor = httpContextAccessor;
            _auditLogWriter = auditLogWriter;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            CaptureChanges(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            CaptureChanges(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            await AfterSaveAsync(eventData.Context);
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            AfterSaveAsync(eventData.Context).GetAwaiter().GetResult();
            return base.SavedChanges(eventData, result);
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            _saving.Clear();
            return base.SaveChangesFailedAsync(eventData, cancellationToken);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            _saving.Clear();
            base.SaveChangesFailed(eventData);
        }

        // A NEW TRANSACTION FORGETS WHAT A PREVIOUS ONE LEFT WITHOUT COMMIT OR ROLLBACK (FOR EXAMPLE, DISPOSED AFTER AN EARLY RETURN)
        public ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
        {
            _waitingCommit.Clear();
            return ValueTask.FromResult(result);
        }

        public DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
        {
            _waitingCommit.Clear();
            return result;
        }

        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            return _auditLogWriter.WriteAsync(TakeWaitingCommit());
        }

        public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        {
            _auditLogWriter.WriteAsync(TakeWaitingCommit()).GetAwaiter().GetResult();
        }

        public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            _waitingCommit.Clear();
            return Task.CompletedTask;
        }

        public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        {
            _waitingCommit.Clear();
        }

        // WITHOUT AN EXPLICIT TRANSACTION THE CHANGE IS ALREADY COMMITTED. INSIDE ONE, THE LOGS WAIT FOR THE COMMIT AND A ROLLBACK DISCARDS THEM
        private async Task AfterSaveAsync(DbContext? context)
        {
            var saved = _saving;
            _saving = new();

            if (context?.Database.CurrentTransaction != null)
            {
                _waitingCommit.AddRange(saved);
                return;
            }

            await _auditLogWriter.WriteAsync(saved);
        }

        private List<AuditLog> TakeWaitingCommit()
        {
            var committed = _waitingCommit;
            _waitingCommit = new();
            return committed;
        }

        // THE USER AND THE IP COME FROM THE REQUEST. WITHOUT A REQUEST (A JOB, A TEST THAT WRITES DIRECTLY) THEY ARE EMPTY
        private void CaptureChanges(DbContext? context)
        {
            if (context == null)
                return;

            context.ChangeTracker.DetectChanges();

            var httpContext = _httpContextAccessor.HttpContext;
            var userIdClaim = httpContext?.User.FindFirst(AuthTokenService.UserIdClaim)?.Value;
            Guid? userId = Guid.TryParse(userIdClaim, out Guid parsedUserId) ? parsedUserId : null;
            var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();

            _saving = context.ChangeTracker.Entries()
                .Select(entry => AuditEntryBuilder.Build(entry, userId, ipAddress))
                .OfType<AuditLog>()
                .ToList();
        }
    }
}
