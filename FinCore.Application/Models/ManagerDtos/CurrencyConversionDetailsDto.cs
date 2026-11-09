using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ManagerDtos
{
    public class CurrencyConversionDetailsDto : CurrencyConversionMonitoringDto
    {
        public string IdempotencyKey { get; set; } = null!;
    }
}
