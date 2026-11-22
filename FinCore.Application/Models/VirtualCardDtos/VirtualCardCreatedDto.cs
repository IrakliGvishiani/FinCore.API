using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.VirtualCardDtos
{
    public class VirtualCardCreatedDto
    {
        public int Id { get; set; }
        public string CardNumber { get; set; } = null!;
        public string Last4Digits { get; set; } = null!;
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public decimal DailyLimit { get; set; }
        public decimal TransactionLimit { get; set; }
        public bool AllowAutoConversion { get; set; }
    }
}
