using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.BankAccountDtos
{
    public class AccountStatementEntryDto
    {
        public int LedgerEntryId { get; set; }

        public string Reference { get; set; } = string.Empty;

        public EntryType Type { get; set; }

        public decimal Amount { get; set; }

        public CurrencyCode Currency { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
