using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.BankAccountDtos
{
    public class UserBankAccountDto
    {
        public string AccountNumber { get; set; } = null!;

        public string Currency { get; set; } = null!;

        public decimal Balance { get; set; }

        public string Status { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
