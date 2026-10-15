using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Exceptions
{
    public class DuplicateKeyException : Exception
    {
        public DuplicateKeyException()
        {
        }
        public DuplicateKeyException(string message) : base(message)
        {
        }
    }
}
