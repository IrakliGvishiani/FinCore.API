using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.UnitTests
{
    public class BankAccountTests
    {
        private static BankAccount NewAccount(decimal balance = 0m)
        {
            var account = BankAccount.Create("GE00TEST0001", "user-1", CurrencyCode.GEL);
            if (balance > 0) account.Credit(balance);
            return account;
        }

        [Fact]
        public void Debit_ReducesBalance()
        {
            var account = NewAccount(100m);          

            account.Debit(30m);                      

            Assert.Equal(70m, account.Balance);      
        }

        [Fact]
        public void Debit_MoreThanBalance_Throws()
        {
            var account = NewAccount(100m);

            Assert.Throws<InsufficientFundsException>(() => account.Debit(100.01m));
        }

        [Theory]                                     
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(10.005)]
        public void Debit_InvalidAmount_Throws(decimal amount)
        {
            var account = NewAccount(100m);

            Assert.Throws<DomainException>(() => account.Debit(amount));
        }

        [Fact]
        public void Debit_FrozenAccount_Throws()
        {
            var account = NewAccount(100m);
            account.Freeze();

            Assert.Throws<DomainException>(() => account.Debit(10m));
        }

        [Fact]
        public void Close_WithNonZeroBalance_Throws()
        {
            var account = NewAccount(50m);

            Assert.Throws<DomainException>(() => account.Close());
        }
    }
}
