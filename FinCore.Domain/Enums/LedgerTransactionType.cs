using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Enums
{
    public enum LedgerTransactionType
    {
        Transfer,
        Deposit,
        Withdrawal,
        CurrencyConversion,
        CardPayment
    }
}
