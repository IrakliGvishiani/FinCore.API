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


        public static VirtualCard Create(
            string userId, BankAccount account,string name,string last4,
            decimal dailyLimit,decimal transactionLimit,DateTime utcNow
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
                CreatedAt = utcNow
            };
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

        public PaymentDeclineReason? Authorize(
          BankAccount account, decimal amount,CurrencyCode paymentCurrency, decimal spentToday, DateTime utcNow)
        {
            if (account.Id != BankAccountId)
                throw new DomainException("Account does not belong to this card.");
            if (amount <= 0 || decimal.Round(amount, 2) != amount)
                throw new DomainException("Amount must be positive with at most 2 decimals.");

            if (Status != VirtualCardStatus.Active) return PaymentDeclineReason.CardNotActive;
            if (IsExpired(utcNow)) return PaymentDeclineReason.CardExpired;
            if (account.Currency != paymentCurrency) return PaymentDeclineReason.CurrencyMismatch;
            if (account.Status != AccountStatus.Active) return PaymentDeclineReason.AccountNotActive;
            if (amount > TransactionLimit) return PaymentDeclineReason.TransactionLimitExceeded;
            if (spentToday + amount > DailyLimit) return PaymentDeclineReason.DailyLimitExceeded;
            if (amount > account.Balance) return PaymentDeclineReason.InsufficientFunds;
            return null;
        }

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
