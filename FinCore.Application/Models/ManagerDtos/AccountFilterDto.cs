using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ManagerDtos
{
    public class AccountFilterDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public string? AccountNumber { get; set; }
        public string? UserId { get; set; }

        public AccountStatus? Status { get; set; }
        public CurrencyCode? Currency { get; set; }
    }
}
