using FinCore.Application.Models.ExchangeDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IConversionService
    {
        Task<ConversionDto> ConvertAsync(
            string userId,string fromNo,string toNo,
            decimal amount,string idempotencyKey,
            decimal? expectedRate = null,
            CancellationToken ct = default
            );

        Task<ConversionQuoteDto> QuoteAsync(
            string userId,string fromNo,string toNo,decimal amount,CancellationToken ct = default
            );

        Task<(IEnumerable<ConversionDto> Items,int TotalCount)> GetMyConversionsAsync(
            string userId,
            ConversionFilter filter,
            int pageNumber = 1,
            int pageSize = 10,
            CancellationToken ct = default
            );
    }
}
