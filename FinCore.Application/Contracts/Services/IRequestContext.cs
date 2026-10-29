using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IRequestContext
    {
        string? IpAddress { get; }
    }
}
