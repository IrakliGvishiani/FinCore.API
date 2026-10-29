using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Entities
{
    public class LedgerEntry
    {

        private LedgerEntry() { }

        internal LedgerEntry(int bankAccountId,EntryType type,decimal amount,CurrencyCode currency,DateTime createdAt)
        {
            BankAccountId = bankAccountId;
            Type = type;
            Amount = amount;
            Currency = currency;
            CreatedAt = createdAt;
        }
        public int Id { get; private set; }

        public int LedgerTransactionId { get; private set; }
        public LedgerTransaction LedgerTransaction { get; private set; } = null!;

        public int? BankAccountId { get; private set; }
        public BankAccount BankAccount { get; private set; } = null!;

        public int? LedgerAccountId { get; private set; }

        public LedgerAccount LedgerAccount { get; private set; } = null!;

        public decimal Amount { get; private set; }

        public EntryType Type { get; private set; }
        public CurrencyCode Currency { get; private set; }

        public DateTime CreatedAt { get; private set; }


        internal LedgerEntry(
            int? bankAccountId,int? ledgerAccountId,
            EntryType type,decimal amount,
            CurrencyCode currency,DateTime createdAt
            )
        {
            if ((bankAccountId is null) == (ledgerAccountId is null))
                throw new DomainException("An entry must target exactly one account.");

            BankAccountId = bankAccountId;
            LedgerAccountId = ledgerAccountId;
            Type = type;
            Amount = amount;
            Currency = currency;
            CreatedAt = createdAt;
        }
    }
}
