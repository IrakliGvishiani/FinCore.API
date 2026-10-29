using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Entities
{
    public class LedgerAccount
    {
        private LedgerAccount() { }

        public int Id { get; private set; }

        public string AccountCode { get; private set; } = null!;

        public string Name { get; private set; } = null!;

        public CurrencyCode Currency { get; private set; }

        public bool IsActive { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public static LedgerAccount Create(
            string accountCode,
            string name,
            CurrencyCode currency
            )
        {

            if (string.IsNullOrWhiteSpace(accountCode))
                throw new DomainException("Account code is required.");

            if (accountCode.Trim().Length > 30)
                throw new DomainException(
                    "Account code cannot exceed 30 characters.");

            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Account name is required.");

            if (name.Trim().Length > 100)
                throw new DomainException(
                    "Account name cannot exceed 100 characters.");

            if (!Enum.IsDefined(typeof(CurrencyCode), currency))
                throw new DomainException("Invalid currency.");

            return new LedgerAccount
            {
                AccountCode = accountCode.Trim().ToUpperInvariant(),
                Name = name.Trim(),
                Currency = currency,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Deactivate()
        {
            IsActive = false;
        }
    }
}
