using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Exceptions
{
    public class ExternalServiceException : Exception
    {
        public ExternalServiceException()
        {

        }

        public ExternalServiceException(string message): base(message)
        {

        }
    }
}
