using LoanSystemAPI.Entities;
using LoanSystemAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LoanSystemAPI.Data
{
    // AUDITS EVERY CHANGE SAVED THROUGH EF, WITHOUT CALLS FROM THE CONTROLLERS THAT SOMEONE WOULD FORGET (D-053). THE LOGS ARE BUILT
    // BEFORE SAVING, WHILE THE ChangeTracker STILL HAS THE ORIGINAL VALUES. BULK OPERATIONS (ExecuteUpdate, ExecuteDelete)
    // DO NOT GO THROUGH THE ChangeTracker AND ARE NOT AUDITED
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AuditLogWriter _auditLogWriter;

        // BUILT IN SavingChanges, WAITING FOR SaveChanges TO SUCCEED
        private List<AuditLog> _saving = new();

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
            await _auditLogWriter.WriteAsync(TakeSaving());
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            _auditLogWriter.WriteAsync(TakeSaving()).GetAwaiter().GetResult();
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

        private List<AuditLog> TakeSaving()
        {
            var saving = _saving;
            _saving = new();
            return saving;
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
