using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.BankAccountDtos
{
    public record OpenAccountRequest(
        CurrencyCode Currency,
        string? Name
        );

}
