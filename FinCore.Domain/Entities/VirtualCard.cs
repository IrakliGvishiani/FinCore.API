using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Entities
{
    public class VirtualCard
    {
        private VirtualCard () { }

        public int Id { get; private set; }

        public string UserId { get; private set; } = null!;
        public int BankAccountId { get; private set; }

        public BankAccount BankAccount { get; private set; } = null!;

        public string Name { get; private set; } = null!;

        public string Last4Digits { get; private set; } = null!;

        public int ExpiryMonth { get; private set; }

        public int ExpiryYear { get; private set; }

        public VirtualCardStatus Status { get; private set; }

        public decimal DailyLimit { get; private set; }

        public decimal TransactionLimit { get; private set; }

        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// When false (default) the card pays only from its main account and only in that account's currency.
        /// When true a payment may be funded from any Active account of the card owner, with FX conversion.
        /// </summary>
        public bool AllowAutoConversion { get; private set; }

        /// <summary>Set on every payment authorization so the row (and its RowVersion) changes.</summary>
        public DateTime? LastAuthorizedAt { get; private set; }

        /// <summary>
        /// Optimistic concurrency token. Every authorization updates the card row, so two concurrent
        /// payments on one card cannot both commit against the same daily-limit snapshot.
        /// </summary>
        public byte[] RowVersion { get; private set; } = null!;


        public static VirtualCard Create(
            string userId, BankAccount account,string name,string last4,
            decimal dailyLimit,decimal transactionLimit,DateTime utcNow,
            bool allowAutoConversion = false
            )
        {
            if (account.UserId != userId)
                throw new DomainException("Card can be created only for your own account.");
            if (account.Status != AccountStatus.Active)
                throw new DomainException("Card requires an active account.");
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 50)
                throw new DomainException("Card name is required (max 50 characters).");
            if (last4.Length != 4 || !last4.All(char.IsDigit))
                throw new DomainException("Invalid card number.");

            if (string.IsNullOrWhiteSpace(last4) ||
                last4.Length != 4 ||
                !last4.All(char.IsDigit))
            {
                throw new DomainException("Invalid card number.");
            }

            EnsureLimits(dailyLimit,transactionLimit);

            var expiry = utcNow.AddYears(3);

            return new VirtualCard
            {
                UserId = userId,
                BankAccountId = account.Id,
                BankAccount = account,
                Name = name.Trim(),
                Last4Digits = last4,
                ExpiryMonth = expiry.Month,
                ExpiryYear = expiry.Year,
                Status = VirtualCardStatus.Active,
                DailyLimit = dailyLimit,
                TransactionLimit = transactionLimit,
                CreatedAt = utcNow,
                AllowAutoConversion = allowAutoConversion
            };
        }

        public void SetAutoConversion(bool enabled)
        {
            if (Status == VirtualCardStatus.Closed)
                throw new DomainException("Closed cards cannot be changed.");

            AllowAutoConversion = enabled;
        }

        public bool IsExpired(DateTime utcNow)
             => utcNow >= new DateTime(ExpiryYear, ExpiryMonth, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);

        public void Freeze()
        {
            if (Status != VirtualCardStatus.Active)
                throw new DomainException("Only active cards can be frozen.");
            Status = VirtualCardStatus.Frozen;
        }

        public void Unfreeze()
        {
            if (Status != VirtualCardStatus.Frozen)
                throw new DomainException("Only frozen cards can be unfrozen.");
            Status = VirtualCardStatus.Active;
        }

        public void Close()
        {
            if (Status == VirtualCardStatus.Closed)
                throw new DomainException("Card is already closed.");
            Status = VirtualCardStatus.Closed;
        }

        public void SetLimits(decimal dailyLimit,decimal transactionLimit)
        {
            if (Status == VirtualCardStatus.Closed)
                throw new DomainException("Closed cards cannot be changed.");

            EnsureLimits(dailyLimit, transactionLimit);

            DailyLimit = dailyLimit;
            TransactionLimit = transactionLimit;
        }

        /// <summary>
        /// Status checks that need no exchange rate, in this fixed order:
        /// CardNotActive, CardExpired, SourceAccountNotAllowed, AutoConversionNotAllowed, AccountNotActive.
        /// </summary>
        /// <param name="source">The account that funds the payment (must belong to the card owner).</param>
        /// <param name="merchantCurrency">Currency the merchant asks for.</param>
        /// <param name="mainAccountCurrency">Currency of the card's main account (<see cref="BankAccountId"/>).</param>
        public PaymentDeclineReason? AuthorizeSource(
            BankAccount source, CurrencyCode merchantCurrency, CurrencyCode mainAccountCurrency, DateTime utcNow)
        {
            if (source is null)
                throw new DomainException("Source account is required.");
            if (source.UserId != UserId)
                throw new DomainException("Source account does not belong to the card owner.");

            if (Status != VirtualCardStatus.Active) return PaymentDeclineReason.CardNotActive;
            if (IsExpired(utcNow)) return PaymentDeclineReason.CardExpired;
            if (!AllowAutoConversion && source.Id != BankAccountId) return PaymentDeclineReason.SourceAccountNotAllowed;
            if (!AllowAutoConversion && merchantCurrency != mainAccountCurrency) return PaymentDeclineReason.AutoConversionNotAllowed;
            if (source.Status != AccountStatus.Active) return PaymentDeclineReason.AccountNotActive;
            return null;
        }

        /// <summary>
        /// Amount checks, after pricing, in this fixed order:
        /// TransactionLimitExceeded, DailyLimitExceeded (both on amounts in the main account's currency),
        /// InsufficientFunds (on the charged amount in the source account's currency).
        /// </summary>
        public PaymentDeclineReason? AuthorizeAmounts(
            BankAccount source, decimal limitAmount, decimal chargedAmount, decimal spentToday)
        {
            if (limitAmount <= 0 || decimal.Round(limitAmount, 2) != limitAmount)
                throw new DomainException("Limit amount must be positive with at most 2 decimals.");
            if (chargedAmount <= 0 || decimal.Round(chargedAmount, 2) != chargedAmount)
                throw new DomainException("Charged amount must be positive with at most 2 decimals.");

            if (limitAmount > TransactionLimit) return PaymentDeclineReason.TransactionLimitExceeded;
            if (spentToday + limitAmount > DailyLimit) return PaymentDeclineReason.DailyLimitExceeded;
            if (chargedAmount > source.Balance) return PaymentDeclineReason.InsufficientFunds;
            return null;
        }

        /// <summary>Touches the card row so its RowVersion changes (see <see cref="RowVersion"/>).</summary>
        public void RegisterAuthorization(DateTime utcNow) => LastAuthorizedAt = utcNow;

        private static void EnsureLimits(decimal daily,decimal perTransaction)
        {
            if (perTransaction <= 0 || daily <= 0)
                throw new DomainException("Limits must be positive.");
            if (perTransaction > daily)
                throw new DomainException("Transaction limit cannot exceed the daily limit.");
            if (daily > 100_000m)
                throw new DomainException("Daily limit is too high.");
        }

    }
}
