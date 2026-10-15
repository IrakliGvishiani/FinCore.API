using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Exceptions
{
    public class ConcurrencyConflictException : Exception
    {
        public ConcurrencyConflictException()
        {

        }
        public ConcurrencyConflictException(string message) : base(message)
        {
        }
    }
}
