using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Exceptions
{
    public class NotFoundException : Exception
    {

        public NotFoundException()
        {

        }
        public NotFoundException(string message) : base(message)
        {
        }
    }
}
