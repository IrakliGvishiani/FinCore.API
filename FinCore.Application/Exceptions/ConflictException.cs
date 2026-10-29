using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Exceptions
{
    public class ConflictException : Exception
    {
        public ConflictException()
        {

        }

        public ConflictException(string message) : base(message)
        {

        }
    }
}
