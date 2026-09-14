using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace LoanSystemAPI.Services
{
    // TURNS ONE CHANGE OF THE ChangeTracker INTO ONE AUDIT LOG
    public static class AuditEntryBuilder
    {
        public static AuditLog? Build(EntityEntry entry, Guid? userId, string? ipAddress)
        {
            // audit_logs WOULD AUDIT ITSELF WITHOUT END
            if (entry.Metadata.ClrType == typeof(AuditLog))
                return null;

            AuditAction action;
            Func<PropertyEntry, object?> value;
            switch (entry.State)
            {
                case EntityState.Added:
                    action = AuditAction.CREATE;
                    value = p => p.CurrentValue;
                    break;
                case EntityState.Modified:
                    action = AuditAction.UPDATE;
                    value = p => p.CurrentValue;
                    break;
                case EntityState.Deleted:
                    action = AuditAction.DELETE;
                    value = p => p.OriginalValue;
                    break;
                default:
                    return null;
            }

            var changes = entry.Properties.ToDictionary(p => p.Metadata.GetColumnName(), value);

            return NewAuditLog(entry, action, changes, userId, ipAddress);
        }

        private static AuditLog NewAuditLog(EntityEntry entry, AuditAction action, Dictionary<string, object?> changes, Guid? userId, string? ipAddress)
        {
            return new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityName = entry.Metadata.GetTableName(),
                EntityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue as Guid?,
                Action = action,
                UserId = userId,
                IpAddress = ipAddress,
                Changes = JsonSerializer.Serialize(changes),
                CreatedDate = DateTime.UtcNow,
            };
        }
    }
}
