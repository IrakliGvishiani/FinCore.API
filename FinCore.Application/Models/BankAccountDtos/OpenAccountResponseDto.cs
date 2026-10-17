using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.BankAccountDtos
{
    public class OpenAccountResponseDto
    {
        public string AccountNumber { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
