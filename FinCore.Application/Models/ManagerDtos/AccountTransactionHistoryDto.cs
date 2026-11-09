using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ManagerDtos
{
    public class AccountTransactionHistoryDto
    {
        public int Id { get; set; }
        public string Reference { get; set; } = null!;
        public EntryType Type { get; set; }
        public decimal Amount { get; set; }
        public CurrencyCode Currency { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
