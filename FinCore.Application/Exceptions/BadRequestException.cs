using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Exceptions
{
    public class BadRequestException : Exception
    {
        public BadRequestException()
        {
        }

        public BadRequestException(string message) : base(message)
        {
        }
    }
}
