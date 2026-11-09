using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.VirtualCardDtos
{
    public class CreatePaymentDto
    {
        public int VirtualCardId { get; set; }
        public decimal Amount { get; set; }
        public string Merchant { get; set; } = null!;
        public string? Description { get; set; }
        public string IdempotencyKey { get; set; } = null!;
    }
}
