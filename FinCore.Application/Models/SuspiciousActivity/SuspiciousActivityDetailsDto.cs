using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.SuspiciousActivity
{
    public class SuspiciousActivityDetailsDto : SuspiciousActivityMonitoringDto
    {
        public string TransferReference { get; set; } = null!;
        public decimal TransferAmount { get; set; }
        public CurrencyCode Currency { get; set; }
        public string FromAccountNumber { get; set; } = null!;
        public string ToAccountNumber { get; set; } = null!;
        public string InitiatedByUserId { get; set; } = null!;
    }
}
