using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.TransferDtos
{
    public class TransferDto
    {
        public int Id { get; set; }

        public string FromAccountNumber { get; set; }

        public string ToAccountNumber { get; set; }

        public decimal Amount { get; set; }
        public CurrencyCode Currency { get; set; }

        public TransferStatus Status { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public string? FailureReason { get; set; }
    }
}
