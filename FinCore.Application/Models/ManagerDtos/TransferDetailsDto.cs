using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ManagerDtos
{
    public class TransferDetailsDto
    {
        public int Id { get; set; }

        public string FromAccountNumber { get; set; } = null!;
        public string ToAccountNumber { get; set; } = null!;

        public string FromUserId { get; set; } = null!;
        public string ToUserId { get; set; } = null!;

        public decimal Amount { get; set; }
        public CurrencyCode Currency { get; set; }

        public TransferStatus Status { get; set; }

        public string? Description { get; set; }
        public string InitiatedByUserId { get; set; } = null!;

        public string? FailureReason { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public List<TransferLedgerEntryDto> LedgerEntries { get; set; } = [];
    }

    public class TransferLedgerEntryDto
    {
        public int Id { get; set; }
        public int? BankAccountId { get; set; }
        public int? LedgerAccountId { get; set; }
        public EntryType Type { get; set; }
        public decimal Amount { get; set; }
    }
}
