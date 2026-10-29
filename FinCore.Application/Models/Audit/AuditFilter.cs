using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.Audit
{
    public class AuditFilter
    {
        public string? UserId { get; set; }
        public string? Action { get; set; }      
        public string? EntityName { get; set; }
        public string? EntityId { get; set; }
        public string? IpAddress { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
