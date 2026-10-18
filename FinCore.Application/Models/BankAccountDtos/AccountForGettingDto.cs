using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.BankAccountDtos
{
    public class AccountForGettingDto
    {
        public string AccountNumber { get; set; } = null!;

        public decimal Balance { get; set; }

        public CurrencyCode Currency { get; set; }

        public AccountStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
