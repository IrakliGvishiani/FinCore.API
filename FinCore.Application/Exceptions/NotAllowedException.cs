using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Exceptions
{
    public class NotAllowedException : Exception
    {
        public NotAllowedException()
        {
        }

        public NotAllowedException(string message) : base(message)
        {
        }
    }
}
