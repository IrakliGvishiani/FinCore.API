using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.VirtualCardDtos
{
    public class PaymentDto
    {
        public int Id { get; set; }
        public int VirtualCardId { get; set; }

        public string Merchant { get; set; } = null!;
        public string? Description { get; set; }

        /// <summary>Amount the merchant asked for, in <see cref="Currency"/>.</summary>
        public decimal Amount { get; set; }
        /// <summary>Merchant currency.</summary>
        public CurrencyCode Currency { get; set; }

        public SimulatedPaymentStatus Status { get; set; }
        public PaymentDeclineReason? FailureReason { get; set; }

        public string Reference { get; set; } = null!;
        public string Last4Digits { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        /// <summary>Account that funded (or would have funded) the payment.</summary>
        public int SourceBankAccountId { get; set; }
        /// <summary>Currency of the source account.</summary>
        public CurrencyCode ChargedCurrency { get; set; }
        /// <summary>Debited amount in <see cref="ChargedCurrency"/>, fee included. Null if declined before pricing.</summary>
        public decimal? ChargedAmount { get; set; }
        /// <summary>Merchant-currency units per 1 charged-currency unit (1 without conversion). Null if declined before pricing.</summary>
        public decimal? ExchangeRate { get; set; }
        /// <summary>FX fee in <see cref="ChargedCurrency"/> (0 without conversion). Null if declined before pricing.</summary>
        public decimal? FeeAmount { get; set; }
        /// <summary>Amount counted against the card limits, in the card's main-account currency. Null if declined before pricing.</summary>
        public decimal? LimitAmount { get; set; }
        /// <summary>Date of the NBG rate table used; null when no rate was needed.</summary>
        public DateTime? RateDate { get; set; }
        /// <summary>True when the charged currency differs from the merchant currency.</summary>
        public bool IsConversion { get; set; }
    }
}
