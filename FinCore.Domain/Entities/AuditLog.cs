using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Entities
{
    public class AuditLog
    {
        public int Id { get; private set; }

        public string? UserId { get; private set; }

        public string Action { get; private set; } = null!;

        public string EntityName { get; private set; } = null!;

        public string? EntityId { get; private set; }

        public string? Details { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public string? IpAddress { get; private set; }

        public static AuditLog Create(string? userId,
            string action,string entityName,string? entityId,
            string? details,string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(action))
                throw new ArgumentException("Action is required.", nameof(action));
            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentException("Entity name is required.", nameof(entityName));

            return new AuditLog
            {
                UserId = userId,
                Action = Trunc(action, 100)!,
                EntityName = Trunc(entityName, 100)!,
                EntityId = Trunc(entityId, 100),
                Details = Trunc(details, 2000),
                IpAddress = Trunc(ipAddress, 45),
                CreatedAt = DateTime.UtcNow
            };
        }

        private static string? Trunc(string? s, int max)
          => s is null || s.Length <= max ? s : s[..max];
    }
}
