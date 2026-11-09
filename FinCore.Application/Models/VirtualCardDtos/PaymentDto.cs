using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.VirtualCardDtos
{
    public class PaymentDto
    {
        public int Id { get; set; }
        public int VirtualCardId { get; set; }

        public string Merchant { get; set; } = null!;
        public string? Description { get; set; }

        public decimal Amount { get; set; }
        public CurrencyCode Currency { get; set; }

        public SimulatedPaymentStatus Status { get; set; }
        public PaymentDeclineReason? FailureReason { get; set; }

        public string Reference { get; set; } = null!;
        public string Last4Digits { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
