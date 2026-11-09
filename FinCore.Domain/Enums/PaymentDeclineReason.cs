using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Enums
{
    public enum PaymentDeclineReason
    {
        CardNotActive, CardExpired, AccountNotActive,
        TransactionLimitExceeded, DailyLimitExceeded, InsufficientFunds,
        CurrencyMismatch
    }
}
