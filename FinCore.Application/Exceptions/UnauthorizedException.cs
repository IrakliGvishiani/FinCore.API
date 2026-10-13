using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Exceptions
{
    public class UnauthorizedException : Exception
    {
        public UnauthorizedException()
        {
        }
        public UnauthorizedException(string message) : base(message)
        {
        }
    }
}
