using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace LoanSystemAPI.Services
{
    // TURNS ONE CHANGE OF THE ChangeTracker INTO ONE AUDIT LOG. changes IS KEYED BY COLUMN AND EVERY FIELD IS AN OBJECT (D-065):
    // CREATE HAS EVERY COLUMN WITH "new", UPDATE ONLY THE COLUMNS THAT CHANGED WITH "old" AND "new", AND DELETE EVERY COLUMN WITH "old"
    public static class AuditEntryBuilder
    {
        private const string StatusProperty = "status";
        private const string DeletedStatus = "DELETED";

        public static AuditLog? Build(EntityEntry entry, Guid? userId, string? ipAddress)
        {
            // audit_logs WOULD AUDIT ITSELF WITHOUT END
            if (entry.Metadata.ClrType == typeof(AuditLog))
                return null;

            AuditAction action;
            Dictionary<string, object?> changes;
            switch (entry.State)
            {
                case EntityState.Added:
                    action = AuditAction.CREATE;
                    changes = EveryColumn(entry, p => Field("new", p.CurrentValue));
                    break;
                // THE LOGICAL DELETE IS AN UPDATE FOR EF, BUT IT IS AUDITED AS WHAT IT MEANS: A DELETE (D-029)
                case EntityState.Deleted:
                case EntityState.Modified when IsLogicalDelete(entry):
                    action = AuditAction.DELETE;
                    changes = EveryColumn(entry, p => Field("old", p.OriginalValue));
                    break;
                case EntityState.Modified:
                    action = AuditAction.UPDATE;
                    changes = AuditedProperties(entry)
                        .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                        .ToDictionary(p => p.Metadata.GetColumnName(), p => (object?)new Dictionary<string, object?> { ["old"] = p.OriginalValue, ["new"] = p.CurrentValue });
                    break;
                default:
                    return null;
            }

            // AN UPDATE WHERE NOTHING REALLY CHANGED LEAVES NO LOG
            if (changes.Count == 0)
                return null;

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

        private static Dictionary<string, object?> EveryColumn(EntityEntry entry, Func<PropertyEntry, object?> field)
        {
            return AuditedProperties(entry).ToDictionary(p => p.Metadata.GetColumnName(), field);
        }

        private static object? Field(string key, object? value)
        {
            return new Dictionary<string, object?> { [key] = value };
        }

        private static IEnumerable<PropertyEntry> AuditedProperties(EntityEntry entry)
        {
            return entry.Properties;
        }

        private static bool IsLogicalDelete(EntityEntry entry)
        {
            var status = entry.Properties.FirstOrDefault(p => string.Equals(p.Metadata.Name, StatusProperty, StringComparison.OrdinalIgnoreCase));

            return status != null
                && status.CurrentValue is Enum currentStatus
                && currentStatus.ToString() == DeletedStatus
                && !Equals(status.OriginalValue, status.CurrentValue);
        }
    }
}
