using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.DomainExceptions
{
    public class DomainException : Exception
    {
        public DomainException(string message) : base(message) { }
    }


    public class InsufficientFundsException : DomainException
    {
        public InsufficientFundsException() : base("Insufficient funds.") { }

        public InsufficientFundsException(string message) : base(message)
        {
        }
    }

}
