using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FinCore.Application.Models.BankAccountDtos
{
    public record RenameAccountRequest(
    [Required, StringLength(50, MinimumLength = 1)] string Name);
}
