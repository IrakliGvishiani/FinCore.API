using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using static FinCore.Domain.DomainExceptions.DomainException;

namespace FinCore.Domain.Entities
{
    public class BankAccount
    {

        private BankAccount() //For EF Core
        {
        }
        private const decimal MaxBalance = 9999999999999999.99m;
        public int  Id { get; private set; }

        public string AccountNumber { get; private set; }

        public string UserId { get; private set; }

        public ApplicationUser User { get; private set; } = null!;
        public CurrencyCode Currency { get; private set; }

        public decimal Balance { get; private set; }

        public static BankAccount Create(string accountNumber, string userId, CurrencyCode currency)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                throw new DomainException("Account number is required.");
            if (string.IsNullOrWhiteSpace(userId))
                throw new DomainException("Account owner is required.");

            if (!Enum.IsDefined(typeof(CurrencyCode), currency))
                throw new DomainException("Invalid currency.");

            //if (string.IsNullOrWhiteSpace(currency))
            //    throw new DomainException("Currency is required.");

            //currency = currency.Trim().ToUpperInvariant();

            //if (currency.Length != 3)
            //    throw new DomainException("Currency code must contain 3 characters.");

            return new BankAccount
            {
                AccountNumber = accountNumber,
                UserId = userId,
                Currency = currency,
                Balance = 0m,
                Status = AccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
        }


        public void Debit(decimal amount)
        {
            EnsureValidAmount(amount);
            if (Status != AccountStatus.Active) throw new DomainException("Cannot debit from an inactive account.");
            if(amount > Balance) throw new InsufficientFundsException("Insufficient funds.");

            Balance -= amount;
        }

        public void Credit(decimal amount)
        {
            EnsureValidAmount(amount);
            if (Status != AccountStatus.Active) throw new DomainException("Cannot credit to an inactive account.");
            if (Balance > MaxBalance - amount)
                throw new DomainException("Balance limit exceeded.");
            Balance += amount;
        }

        public void Freeze()
        {
            if (Status != AccountStatus.Active)
                throw new DomainException("Only active accounts can be frozen.");
            Status = AccountStatus.Frozen;
        }

        public void Unfreeze()
        {
            if (Status != AccountStatus.Frozen)
                throw new DomainException("Only frozen accounts can be unfrozen.");
            Status = AccountStatus.Active;
        }


        public void Close()
        {
            if (Status != AccountStatus.Active)
                throw new DomainException(
                    "Only active accounts can be closed.");

            if (Balance != 0)
                throw new DomainException(
                    "Cannot close account with non-zero balance.");

            Status = AccountStatus.Closed;
        }

        private static void EnsureValidAmount(decimal amount)
        {
            if (amount <= 0)
                throw new DomainException("Amount must be greater than zero.");
            if (decimal.Round(amount, 2) != amount)
                throw new DomainException("Amount cannot have more than 2 decimal places.");
        }

        public ICollection<Transfer> OutgoingTransfers { get; private set; }
    = new List<Transfer>();

        public ICollection<Transfer> IncomingTransfers { get; private set; }
            = new List<Transfer>();

        public ICollection<LedgerEntry> LedgerEntries { get; private set; }
    = new List<LedgerEntry>();

        public AccountStatus Status { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public byte[] RowVersion { get; private set; } = null!;
    }
}
