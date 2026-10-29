using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.UnitTests
{
    public class LedgerTransactionTests
    {
        [Fact]
        public void CreateSingleEntry_Debit_CreatesOneDebitEntry()
        {
            var tx = LedgerTransaction.CreateSingleEntry(
                "FX-1-OUT", CurrencyCode.GEL, 1, EntryType.Debit, 100m);

            var entry = Assert.Single(tx.Entries);
            Assert.Equal(EntryType.Debit, entry.Type);
            Assert.Equal(100m, entry.Amount);
            Assert.Equal(CurrencyCode.GEL, entry.Currency);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(10.005)]
        public void CreateSingleEntry_InvalidAmount_Throws(decimal amount)
        {
            Assert.Throws<DomainException>(() =>
                LedgerTransaction.CreateSingleEntry("ref", CurrencyCode.GEL, 1, EntryType.Credit, amount));
        }

        [Fact]
        public void CreateSingleEntry_EmptyReference_Throws()
        {
            Assert.Throws<DomainException>(() =>
                LedgerTransaction.CreateSingleEntry(" ", CurrencyCode.GEL, 1, EntryType.Credit, 10m));
        }
    }
}
