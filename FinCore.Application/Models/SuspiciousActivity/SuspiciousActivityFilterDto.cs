using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.SuspiciousActivity
{
    public class SuspiciousActivityFilterDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public SuspiciousActivityStatus? Status { get; set; }
        public RiskLevel? RiskLevel { get; set; }
        public string? UserId { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
