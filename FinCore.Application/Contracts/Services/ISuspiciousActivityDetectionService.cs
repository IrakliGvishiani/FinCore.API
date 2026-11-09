using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface ISuspiciousActivityDetectionService
    {
        Task CheckTransferAsync(
            int transferId,
            decimal balanceBeforeTransfer,
            CancellationToken ct = default);
            
    }
}
