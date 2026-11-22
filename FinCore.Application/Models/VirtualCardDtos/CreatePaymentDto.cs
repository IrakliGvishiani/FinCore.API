using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.VirtualCardDtos
{
    public class CreatePaymentDto
    {
        public int VirtualCardId { get; set; }

        /// <summary>Amount in the merchant currency (<see cref="Currency"/>).</summary>
        public decimal Amount { get; set; }
        public string Merchant { get; set; } = null!;
        public string? Description { get; set; }
        public string IdempotencyKey { get; set; } = null!;

        /// <summary>Merchant currency. Optional: defaults to the currency of the card's main account.</summary>
        public CurrencyCode? Currency { get; set; }

        /// <summary>Account that funds the payment. Optional: defaults to the card's main account.</summary>
        public int? SourceAccountId { get; set; }

        /// <summary>
        /// Optional rate protection: the charged amount the client was quoted (in the source account's currency).
        /// If the server computes a different amount the payment is rejected with 409 and nothing is stored.
        /// </summary>
        public decimal? ExpectedChargedAmount { get; set; }
    }
}
