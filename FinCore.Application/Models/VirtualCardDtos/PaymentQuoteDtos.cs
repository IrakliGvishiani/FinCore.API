using FinCore.Domain.Enums;

namespace FinCore.Application.Models.VirtualCardDtos
{
    public class PaymentQuoteRequestDto
    {
        public int VirtualCardId { get; set; }
        /// <summary>Amount in the merchant currency.</summary>
        public decimal Amount { get; set; }
        /// <summary>Merchant currency (required).</summary>
        public CurrencyCode? Currency { get; set; }
    }

    public class PaymentQuoteDto
    {
        public int VirtualCardId { get; set; }
        public decimal MerchantAmount { get; set; }
        public CurrencyCode MerchantCurrency { get; set; }
        public bool AllowAutoConversion { get; set; }
        public int MainAccountId { get; set; }

        /// <summary>Amount counted against the card limits, in <see cref="LimitCurrency"/>. Null when no account is usable.</summary>
        public decimal? LimitAmount { get; set; }
        /// <summary>Currency of the card's main account (the limit currency).</summary>
        public CurrencyCode LimitCurrency { get; set; }
        public decimal TransactionLimit { get; set; }
        public decimal DailyLimit { get; set; }
        public decimal SpentToday { get; set; }
        public bool WithinTransactionLimit { get; set; }
        public bool WithinDailyLimit { get; set; }

        /// <summary>Accounts the card may use for this payment: main account first, then by currency code, then id.</summary>
        public List<PaymentQuoteAccountDto> Accounts { get; set; } = new();

        /// <summary>
        /// Deterministic recommendation: same-currency account with enough balance (main account first, then lowest id),
        /// else the main account if enough, else another account with enough balance ordered by currency code then id.
        /// Null when no account has enough balance.
        /// </summary>
        public int? RecommendedAccountId { get; set; }

        /// <summary>When no account is usable: the reason a payment would be declined right now.</summary>
        public PaymentDeclineReason? DeclineReason { get; set; }
    }

    public class PaymentQuoteAccountDto
    {
        public int AccountId { get; set; }
        public string AccountNumber { get; set; } = null!;
        public string AccountName { get; set; } = null!;
        public CurrencyCode Currency { get; set; }
        public bool IsMainAccount { get; set; }

        /// <summary>Amount that would be debited, in <see cref="Currency"/>, fee included.</summary>
        public decimal ChargedAmount { get; set; }
        /// <summary>Merchant-currency units per 1 unit of <see cref="Currency"/>; 1 without conversion.</summary>
        public decimal ExchangeRate { get; set; }
        public decimal FeeRate { get; set; }
        public decimal FeeAmount { get; set; }
        public DateTime? RateDate { get; set; }
        public bool IsConversion { get; set; }
        public bool HasSufficientBalance { get; set; }
    }
}
