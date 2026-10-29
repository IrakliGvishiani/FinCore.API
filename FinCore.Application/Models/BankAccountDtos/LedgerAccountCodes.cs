using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.BankAccountDtos
{
    public static class LedgerAccountCodes
    {
        public static string Cash(CurrencyCode c) => $"CASH-{c}";   
        public static string Fx(CurrencyCode c) => $"FX-{c}";

        public static string Fee(CurrencyCode c) => $"FEE-{c}";
    }
}
