using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Exceptions
{
    public class InternalServerException : Exception
    {
        public InternalServerException()
        {
        }
        public InternalServerException(string message) : base(message)
        {
        }
    }
}
