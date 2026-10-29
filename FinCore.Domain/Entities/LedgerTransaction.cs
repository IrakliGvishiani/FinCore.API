using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Entities
{

    public readonly record struct LedgerLine(
    int? BankAccountId, int? LedgerAccountId, EntryType Type, decimal Amount)
    {
        public static LedgerLine ForCustomer(int bankAccountId, EntryType type, decimal amount)
            => new(bankAccountId, null, type, amount);

        public static LedgerLine ForSystem(int ledgerAccountId, EntryType type, decimal amount)
            => new(null, ledgerAccountId, type, amount);
    }
    

    public class LedgerTransaction
    {
        private readonly List<LedgerEntry> _entries = new();

        private LedgerTransaction() { } // For EF Core

        public int Id { get; private set; }

        public int? TransferId { get; private set; }
        public Transfer? Transfer { get; private set; }

        public CurrencyCode Currency { get; private set; }
        public string Reference { get; private set; } = null!;

        public DateTime CreatedAt { get; private set; }

        public IReadOnlyCollection<LedgerEntry> Entries => _entries.AsReadOnly();

        public static LedgerTransaction Create(
            string reference,
            CurrencyCode currency,
            IEnumerable<LedgerLine> lines,
            int? transferId = null
            )
        {
            if (transferId.HasValue && transferId.Value <= 0)
                throw new DomainException(
                    "Transfer ID must be positive.");

            if (!Enum.IsDefined(typeof(CurrencyCode), currency))
                throw new DomainException("Invalid currency.");

            if (string.IsNullOrWhiteSpace(reference))
                throw new DomainException("Reference is required.");

            reference = reference.Trim();

            if (reference.Length > 100)
                throw new DomainException("Reference cannot exceed 100 characters.");

            var list = lines?.ToList() ?? throw new ArgumentNullException(nameof(lines));

            if(list.Count < 2)
                throw new DomainException("A ledger transaction must have at least two entries.");

            foreach (var l in list)
            {
                if (!Enum.IsDefined(typeof(EntryType), l.Type))
                    throw new DomainException("Invalid ledger entry type.");

                if (l.BankAccountId <= 0)
                    throw new DomainException("Bank account ID must be positive.");

                if (l.Amount <= 0 || decimal.Round(l.Amount, 2) != l.Amount)
                    throw new DomainException("Entry amount must be positive with at most 2 decimals.");
            }
            var debits = list.Where(l => l.Type == EntryType.Debit).Sum(l => l.Amount);
            var credits = list.Where(l => l.Type == EntryType.Credit).Sum(l => l.Amount);

            if (debits != credits)
                throw new DomainException("Debits and credits must balance.");

            if (!list.Any(l => l.Type == EntryType.Debit) ||
               !list.Any(l => l.Type == EntryType.Credit))
            {
                throw new DomainException(
                    "A ledger transaction must contain both debit and credit entries.");
            }

            var now = DateTime.UtcNow;


            var tx = new LedgerTransaction
            {
                Reference = reference.Trim(),
                Currency = currency,
                CreatedAt = now,
                TransferId = transferId
            };

            foreach (var l in list)
                tx._entries.Add(new LedgerEntry(l.BankAccountId,l.LedgerAccountId, l.Type, l.Amount, currency, now));

            return tx;
        }

        public static LedgerTransaction CreateDeposit(
    string reference, BankAccount customer, LedgerAccount cash, decimal amount)
        {
            if (!cash.IsActive)
                throw new DomainException("Cash ledger account is inactive.");
            if (customer.Currency != cash.Currency)
                throw new DomainException("Ledger account currency must match the account currency.");

            return Create(reference, customer.Currency, new[]
            {
        LedgerLine.ForSystem(cash.Id, EntryType.Debit, amount),
        LedgerLine.ForCustomer(customer.Id, EntryType.Credit, amount)
    });
        }

        public static LedgerTransaction CreateSingleEntry(
            string reference,
            CurrencyCode currency,
            int bankAccountId,
            EntryType type,
            decimal amount)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new DomainException("Reference is required.");
            if (!Enum.IsDefined(currency))
                throw new DomainException("Invalid currency.");
            if (!Enum.IsDefined(type))
                throw new DomainException("Invalid entry type.");
            if (amount <= 0 || decimal.Round(amount, 2) != amount)
                throw new DomainException("Amount must be positive with at most 2 decimals.");

            var now = DateTime.UtcNow;

            var tx = new LedgerTransaction
            {
                Reference = reference.Trim(),
                Currency = currency,
                TransferId = null,          
                CreatedAt = now
            };

            tx._entries.Add(new LedgerEntry(bankAccountId, type, amount, currency, now));
            return tx;
        }


    }

}
