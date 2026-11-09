using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ManagerDtos
{
    public class TransferMonitoringDto
    {
        public int Id { get; set; }

        public string FromAccountNumber { get; set; } = null!;
        public string ToAccountNumber { get; set; } = null!;

        public decimal Amount { get; set; }
        public CurrencyCode Currency { get; set; }

        public TransferStatus Status { get; set; }

        public string? Description { get; set; }

        public string InitiatedByUserId { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
