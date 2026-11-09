using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.VirtualCardDtos
{
    public class SetVirtualCardLimitsDto
    {
        public decimal DailyLimit { get; set; } = 1000;
        public decimal TransactionLimit { get; set; } = 400;
    }
}
