using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.SuspiciousActivity
{
    public class SuspiciousActivityMonitoringDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public int TransferId { get; set; }
        public string Reason { get; set; } = null!;
        public SuspiciousActivityStatus Status { get; set; }
        public int RiskScore { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }
}
