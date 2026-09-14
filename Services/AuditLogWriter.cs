using LoanSystemAPI.Data;
using LoanSystemAPI.Entities;

namespace LoanSystemAPI.Services
{
    // SAVES AUDIT LOGS WITH ITS OWN DbContext, SO THEY NEVER GO IN THE SAME TRANSACTION AS THE BUSINESS CHANGE
    public class AuditLogWriter
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditLogWriter> _logger;

        public AuditLogWriter(IServiceScopeFactory scopeFactory, ILogger<AuditLogWriter> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task WriteAsync(IReadOnlyCollection<AuditLog> auditLogs)
        {
            if (auditLogs.Count == 0)
                return;

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.AuditLogs.AddRangeAsync(auditLogs);
            await dbContext.SaveChangesAsync();
        }
    }
}
