using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.BankAccountDtos
{
    public class RecipientAccountDto
    {
        public int AccountId { get; set; }

        public string MaskedAccountNumber { get; set; } = null!;

        public CurrencyCode Currency { get; set; }
    }
}
