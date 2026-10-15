using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Entities
{
    public class Transfer
    {
        private Transfer() // For EF Core
        {
        }

        public int Id { get; private set; }

        public int FromAccountId { get; private set; }
        public BankAccount FromAccount { get; private set; } = null!;

        public int ToAccountId { get; private set; }
        public BankAccount ToAccount { get; private set; } = null!;

        public decimal Amount { get; private set; }

        public CurrencyCode Currency { get; private set; }

        public TransferStatus Status { get; private set; }

        public string? Description { get; private set; }

        public string IdempotencyKey { get; private set; } = null!;

        public DateTime CreatedAt { get; private set; }

        public DateTime? CompletedAt { get; private set; }

        public string? FailureReason { get; private set; }

        public void Complete()
        {
            if (Status != TransferStatus.Pending)
                throw new DomainException("Only pending transfers can be completed.");
            Status = TransferStatus.Completed;
            CompletedAt = DateTime.UtcNow;
        }

        public void Fail(string reason)
        {
            if (Status != TransferStatus.Pending)
                throw new DomainException(
                    "Only pending transfers can be failed.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new DomainException(
                    "Failure reason is required.");

            reason = reason.Trim();

            if (reason.Length > 500)
                throw new DomainException(
                    "Failure reason cannot exceed 500 characters.");

            Status = TransferStatus.Failed;
            FailureReason = reason;
            CompletedAt = DateTime.UtcNow;
        }

        public LedgerTransaction Execute(BankAccount from, BankAccount to)
        {
            if (Status != TransferStatus.Pending)
                throw new DomainException("Only pending transfers can be executed.");
            if (from is null || to is null)
                throw new DomainException(
                    "Source and destination accounts are required.");

            if (from.Id != FromAccountId || to.Id != ToAccountId)
                throw new DomainException(
                    "Accounts do not match this transfer.");

            from.Debit(Amount);
            to.Credit(Amount);

            var ledgerTx = LedgerTransaction.Create(
                reference: $"TRF-{IdempotencyKey}",
                currency: Currency,
                lines: new[]
                {
                    new LedgerLine(from.Id, EntryType.Debit, Amount),
                    new LedgerLine(to.Id, EntryType.Credit, Amount)
                },
                transferId: Id
                );

            Complete();
            return ledgerTx;
        }
        public static Transfer Create(
            BankAccount fromAccount,
            BankAccount toAccount,
            decimal amount,
            string idempotencyKey,
            string? description = null)
        {
            if (fromAccount is null)
                throw new ArgumentNullException(nameof(fromAccount));

            if (toAccount is null)
                throw new ArgumentNullException(nameof(toAccount));

            if (fromAccount.Id == toAccount.Id)
                throw new DomainException(
                    "Source and destination accounts must be different.");

            if (amount <= 0 || decimal.Round(amount, 2) != amount)
                throw new DomainException(
                    "Amount must be positive and have at most 2 decimal places.");

            if (fromAccount.Currency != toAccount.Currency)
                throw new DomainException(
                    "Account currencies must match.");

            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new DomainException(
                    "Idempotency key is required.");

            if (description?.Length > 500)
                throw new DomainException(
                    "Description cannot exceed 500 characters.");

            return new Transfer
            {
                FromAccount = fromAccount,
                FromAccountId = fromAccount.Id,
                ToAccount = toAccount,
                ToAccountId = toAccount.Id,
                Amount = amount,
                Currency = fromAccount.Currency,
                IdempotencyKey = idempotencyKey,
                Description = description?.Trim(),
                Status = TransferStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
