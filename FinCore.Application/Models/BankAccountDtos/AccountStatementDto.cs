using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.BankAccountDtos
{
    public class AccountStatementDto
    {
        public IEnumerable<AccountStatementEntryDto> Items { get; set; } = Enumerable.Empty<AccountStatementEntryDto>();

        public int TotalCount { get; set; }
    }
}
