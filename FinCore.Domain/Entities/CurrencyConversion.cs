using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Entities
{
    public class CurrencyConversion
    {
        private CurrencyConversion() { }

        public int Id { get; private set; }
        public string InitiatedByUserId { get; private set; } = null!;
        public int FromAccountId { get; private set; }
        public int ToAccountId { get; private set; }
        public BankAccount FromAccount { get; private set; } = null!;
        public BankAccount ToAccount { get; private set; } = null!;
        public CurrencyCode FromCurrency { get; private set; }
        public CurrencyCode ToCurrency { get; private set; }
        public decimal FromAmount { get; private set; }
        public decimal ToAmount { get; private set; }
        public decimal FeeRate { get; private set; }      
        public decimal FeeAmount { get; private set; }    
        public decimal NetAmount => FromAmount - FeeAmount;   
        public decimal Rate { get; private set; }
        public DateTime RateDate { get; private set; }
        public string Reference { get; private set; } = null!;
        public string IdempotencyKey { get; private set; } = null!;
        public DateTime CreatedAt { get; private set; }

        public static CurrencyConversion Create(
            string userId,BankAccount from,BankAccount to,
            decimal fromAmount,decimal rate,DateTime rateDate,decimal feeRate,
            string idempotencyKey
            )
        {
            if (from.UserId != userId || to.UserId != userId)
                throw new DomainException("Conversion is allowed only between your own accounts.");
            if (from.AccountNumber == to.AccountNumber || from.Currency == to.Currency)
                throw new DomainException("Accounts must have different currencies.");
            if (fromAmount <= 0 || decimal.Round(fromAmount, 2) != fromAmount)
                throw new DomainException("Amount must be positive with at most 2 decimals.");
            if (rate <= 0)
                throw new DomainException("Invalid exchange rate.");

            if (feeRate < 0 || feeRate >= 1)
                throw new DomainException("Invalid fee rate.");

            var feeAmount = decimal.Round(fromAmount * feeRate, 2, MidpointRounding.AwayFromZero);
            var netAmount = fromAmount - feeAmount;

            var toAmount = decimal.Round(netAmount * rate, 2, MidpointRounding.ToZero);
            if (toAmount <= 0)
                throw new DomainException("Amount is too small to convert.");

            return new CurrencyConversion
            {
                InitiatedByUserId = userId,
                FromAccountId = from.Id,
                ToAccountId = to.Id,
                FromAccount = from,
                ToAccount = to,
                FromCurrency = from.Currency,
                ToCurrency = to.Currency,
                FromAmount = fromAmount,
                ToAmount = toAmount,
                FeeRate = feeRate,
                FeeAmount = feeAmount,
                Rate = rate,
                RateDate = rateDate,
                Reference = $"FX-{Guid.NewGuid():N}",
                IdempotencyKey = idempotencyKey,
                CreatedAt = DateTime.UtcNow
            };
        }

        public IReadOnlyList<LedgerTransaction> Execute(BankAccount from, BankAccount to,
            LedgerAccount fxFrom,LedgerAccount fxTo, LedgerAccount feeAccount
            )
        {
            if (fxFrom.Currency != FromCurrency
            || fxTo.Currency != ToCurrency
            || feeAccount.Currency != FromCurrency)
                throw new DomainException("Ledger account currency mismatch.");

            from.Debit(FromAmount);
            to.Credit(ToAmount);
            var outLines = new List<LedgerLine>
            {
                LedgerLine.ForCustomer(from.Id, EntryType.Debit,  FromAmount),
                LedgerLine.ForSystem(fxFrom.Id,  EntryType.Credit, NetAmount)
            };

            if (FeeAmount > 0)
                outLines.Add(LedgerLine.ForSystem(feeAccount.Id, EntryType.Credit, FeeAmount));


             return new[]
            {
                LedgerTransaction.Create($"{Reference}-OUT", FromCurrency, outLines),
                LedgerTransaction.Create($"{Reference}-IN", ToCurrency, new[]
                {
                    LedgerLine.ForSystem(fxTo.Id, EntryType.Debit,  ToAmount),
                    LedgerLine.ForCustomer(to.Id, EntryType.Credit, ToAmount)
                })
            };
         

        }
    }
}
