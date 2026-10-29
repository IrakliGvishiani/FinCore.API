using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ExchangeDtos
{
    public class ConversionOptions
    {
        public const string SectionName = "Conversion";
        public decimal FeeRate { get; set; } = 0.005m; // 0.5%
    }
}
