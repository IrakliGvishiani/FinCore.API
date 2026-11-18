using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IReceiptService
    {
        Task<byte[]> GeneratePaymentReceiptAsync(
            string userId,
            int paymentId,
            CancellationToken ct = default
            );

        Task<byte[]> GenerateCurrencyConversionReceiptAsync(
            string userId,
            int paymentId,
            CancellationToken ct = default
            );

        Task<byte[]> GenerateTransferReceiptAsync(
            string userId,
            int transferId,
            CancellationToken ct = default);
    }
}
