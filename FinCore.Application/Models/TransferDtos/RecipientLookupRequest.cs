using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FinCore.Application.Models.TransferDtos
{
    public record RecipientLookupRequest(
    [Required, RegularExpression(@"^\d{11}$")] string PersonalNumber);
}
