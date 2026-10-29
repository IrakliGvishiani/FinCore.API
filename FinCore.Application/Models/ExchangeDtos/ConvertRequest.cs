using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FinCore.Application.Models.ExchangeDtos
{
    public record ConvertRequest(
    [Required] string FromAccountNumber, [Required] string ToAccountNumber,
    [Range(0.01, 1_000_000)] decimal Amount, decimal? ExpectedRate);
}
