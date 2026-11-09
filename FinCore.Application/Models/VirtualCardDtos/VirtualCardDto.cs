using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.VirtualCardDtos
{
    public class VirtualCardDto
    {
        public int Id { get; set; }
        public int BankAccountId { get; set; }
        public string Name { get; set; } = null!;
        public string Last4Digits { get; set; } = null!;
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public VirtualCardStatus Status { get; set; }
        public decimal DailyLimit { get; set; }
        public decimal TransactionLimit { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
