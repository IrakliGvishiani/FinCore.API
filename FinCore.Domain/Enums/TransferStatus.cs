using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Enums
{
    public enum TransferStatus
    {
        Pending = 0,
        Completed = 1,
        Failed = 2,
        Cancelled = 3
    }
}
