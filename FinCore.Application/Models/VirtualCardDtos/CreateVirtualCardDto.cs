using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.VirtualCardDtos
{
    public class CreateVirtualCardDto
    {
        public int? BankAccountId { get; set; }
        public string Name { get; set; } = null!;
        public decimal DailyLimit { get; set; }
        public decimal TransactionLimit { get; set; }
    }
}
