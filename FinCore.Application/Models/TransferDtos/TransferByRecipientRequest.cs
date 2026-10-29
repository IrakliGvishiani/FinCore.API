using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FinCore.Application.Models.TransferDtos
{
    public record TransferByRecipientRequest(
    [Required, StringLength(20)] string FromAccountNumber,
    [Range(1, int.MaxValue)] int ToAccountId,
    [Range(0.01, 1_000_000)] decimal Amount,
    [StringLength(500)] string? Description);
}
