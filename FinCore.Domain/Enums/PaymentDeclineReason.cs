using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Enums
{
    public enum PaymentDeclineReason
    {
        // Stored as strings: never rename or remove a member.
        CardNotActive, CardExpired, AccountNotActive,
        TransactionLimitExceeded, DailyLimitExceeded, InsufficientFunds,
        /// <summary>Legacy; no longer produced (payments are always in the account currency or converted).</summary>
        CurrencyMismatch,
        /// <summary>Merchant currency differs from the card's main account currency and the card has auto-conversion off.</summary>
        AutoConversionNotAllowed,
        /// <summary>A source account other than the card's main account was requested and auto-conversion is off.</summary>
        SourceAccountNotAllowed
    }
}
