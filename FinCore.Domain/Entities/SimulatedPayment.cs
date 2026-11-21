using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Entities
{
    public class SimulatedPayment
    {
        private SimulatedPayment() { }

        public int Id { get; private set; }
        public int VirtualCardId { get; private set; }

        /// <summary>Amount the merchant asked for, in <see cref="Currency"/> (the merchant currency).</summary>
        public decimal Amount { get; private set; }
        /// <summary>Merchant currency.</summary>
        public CurrencyCode Currency { get; private set; }

        public SimulatedPaymentStatus Status { get; private set; }
        public PaymentDeclineReason? FailureReason { get; private set; }
        public string Merchant { get; private set; } = null!;
        public string? Description { get; private set; }
        public string Reference { get; private set; } = null!;
        public string IdempotencyKey { get; private set; } = null!;
        public DateTime CreatedAt { get; private set; }

        /// <summary>Account that funds (or would have funded) the payment.</summary>
        public int SourceBankAccountId { get; private set; }
        /// <summary>Currency of the source account.</summary>
        public CurrencyCode ChargedCurrency { get; private set; }

        // Pricing. Null only for payments declined before pricing (status checks).
        /// <summary>Amount debited (or that would be debited) from the source account, in <see cref="ChargedCurrency"/>, fee included.</summary>
        public decimal? ChargedAmount { get; private set; }
        /// <summary>Merchant-currency units per 1 charged-currency unit; 1 when no conversion.</summary>
        public decimal? ExchangeRate { get; private set; }
        /// <summary>FX fee in <see cref="ChargedCurrency"/>, part of <see cref="ChargedAmount"/>; 0 when no conversion.</summary>
        public decimal? FeeAmount { get; private set; }
        /// <summary>Payment amount in the card's main-account currency, used for card limits.</summary>
        public decimal? LimitAmount { get; private set; }
        /// <summary>Date of the rate table used; null when no rate was needed.</summary>
        public DateTime? RateDate { get; private set; }

        public bool IsPriced => ChargedAmount.HasValue;
        public bool IsConversion => ChargedCurrency != Currency;

        public static SimulatedPayment Create(
            VirtualCard card, BankAccount sourceAccount, CurrencyCode currency, decimal amount,
            string merchant, string? description, string idempotencyKey,
            DateTime utcNow
            )
        {
            if (string.IsNullOrWhiteSpace(merchant) || merchant.Trim().Length > 100)
                throw new DomainException("Merchant is required (max 100 characters).");

            if (card is null)
                throw new DomainException("Virtual card is required.");

            if (sourceAccount is null)
                throw new DomainException("Source account is required.");

            if (sourceAccount.UserId != card.UserId)
                throw new DomainException("Source account does not belong to the card owner.");

            if (!Enum.IsDefined(currency))
                throw new DomainException("Invalid currency.");

            if (amount <= 0 || decimal.Round(amount, 2) != amount)
                throw new DomainException("Invalid payment amount.");

            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new DomainException("Idempotency key is required.");
            if (idempotencyKey.Length > 100)
                throw new DomainException("Idempotency key is too long.");


            return new SimulatedPayment
            {
                VirtualCardId = card.Id,
                Amount = amount,
                Currency = currency,
                SourceBankAccountId = sourceAccount.Id,
                ChargedCurrency = sourceAccount.Currency,
                Status = SimulatedPaymentStatus.Pending,
                FailureReason = null,
                Merchant = merchant.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Reference = $"PAY-{Guid.NewGuid():N}",
                IdempotencyKey = idempotencyKey.Trim(),
                CreatedAt = utcNow
            };
        }

        /// <summary>Records the price of a pending payment. Can be applied once.</summary>
        public void ApplyPricing(
            decimal chargedAmount, decimal exchangeRate, decimal feeAmount, decimal limitAmount, DateTime? rateDate)
        {
            if (Status != SimulatedPaymentStatus.Pending)
                throw new DomainException("Only pending payments can be priced.");
            if (IsPriced)
                throw new DomainException("Payment is already priced.");

            EnsureMoney(chargedAmount, "Charged amount");
            EnsureMoney(limitAmount, "Limit amount");
            if (exchangeRate <= 0)
                throw new DomainException("Invalid exchange rate.");
            if (feeAmount < 0 || decimal.Round(feeAmount, 2) != feeAmount || feeAmount >= chargedAmount)
                throw new DomainException("Invalid fee amount.");

            if (!IsConversion && (chargedAmount != Amount || exchangeRate != 1m || feeAmount != 0m))
                throw new DomainException("A payment without conversion is charged exactly its amount.");

            ChargedAmount = chargedAmount;
            ExchangeRate = exchangeRate;
            FeeAmount = feeAmount;
            LimitAmount = limitAmount;
            RateDate = rateDate;
        }

        /// <summary>
        /// Debits the source account, completes the payment and returns the ledger transactions,
        /// each balanced in its own currency:
        /// <list type="bullet">
        /// <item>No conversion: one transaction <c>{Reference}</c> — customer debit / card settlement credit.</item>
        /// <item>Conversion: <c>{Reference}-OUT</c> in the charged currency — customer debit / FX credit (net) / fee credit;
        /// <c>{Reference}-IN</c> in the merchant currency — FX debit / card settlement credit.</item>
        /// </list>
        /// </summary>
        public IReadOnlyList<LedgerTransaction> Settle(
            BankAccount source, LedgerAccount cardSettlement,
            LedgerAccount? fxSource = null, LedgerAccount? fxMerchant = null, LedgerAccount? feeAccount = null)
        {
            if (Status != SimulatedPaymentStatus.Pending)
                throw new DomainException("Only pending payments can be settled.");
            if (!IsPriced)
                throw new DomainException("Payment must be priced before settlement.");
            if (source.Id != SourceBankAccountId || source.Currency != ChargedCurrency)
                throw new DomainException("Source account does not match the payment.");
            if (cardSettlement.Currency != Currency)
                throw new DomainException("Ledger account currency mismatch.");

            var charged = ChargedAmount!.Value;
            var fee = FeeAmount!.Value;

            IReadOnlyList<LedgerTransaction> transactions;

            if (!IsConversion)
            {
                transactions = new[]
                {
                    LedgerTransaction.Create(Reference, Currency, new[]
                    {
                        LedgerLine.ForCustomer(source.Id, EntryType.Debit, charged),
                        LedgerLine.ForSystem(cardSettlement.Id, EntryType.Credit, charged)
                    }, LedgerTransactionType.CardPayment)
                };
            }
            else
            {
                if (fxSource is null || fxMerchant is null || feeAccount is null)
                    throw new DomainException("FX and fee ledger accounts are required for a converted payment.");
                if (fxSource.Currency != ChargedCurrency
                    || fxMerchant.Currency != Currency
                    || feeAccount.Currency != ChargedCurrency)
                    throw new DomainException("Ledger account currency mismatch.");

                var outLines = new List<LedgerLine>
                {
                    LedgerLine.ForCustomer(source.Id, EntryType.Debit, charged),
                    LedgerLine.ForSystem(fxSource.Id, EntryType.Credit, charged - fee)
                };
                if (fee > 0)
                    outLines.Add(LedgerLine.ForSystem(feeAccount.Id, EntryType.Credit, fee));

                transactions = new[]
                {
                    LedgerTransaction.Create($"{Reference}-OUT", ChargedCurrency, outLines, LedgerTransactionType.CardPayment),
                    LedgerTransaction.Create($"{Reference}-IN", Currency, new[]
                    {
                        LedgerLine.ForSystem(fxMerchant.Id, EntryType.Debit, Amount),
                        LedgerLine.ForSystem(cardSettlement.Id, EntryType.Credit, Amount)
                    }, LedgerTransactionType.CardPayment)
                };
            }

            source.Debit(charged);
            Status = SimulatedPaymentStatus.Completed;

            return transactions;
        }

        public void Fail(PaymentDeclineReason reason)
        {
            if (Status != SimulatedPaymentStatus.Pending)
                throw new DomainException("Only pending payments can be failed.");

            Status = SimulatedPaymentStatus.Failed;
            FailureReason = reason;
        }

        private static void EnsureMoney(decimal value, string name)
        {
            if (value <= 0 || decimal.Round(value, 2) != value)
                throw new DomainException($"{name} must be positive with at most 2 decimals.");
        }
    }
}
