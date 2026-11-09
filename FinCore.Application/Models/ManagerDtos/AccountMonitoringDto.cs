using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ManagerDtos
{
    public class AccountMonitoringDto
    {
        public int Id { get; set; }

        public string AccountNumber { get; set; } = null!;
        public string Name { get; set; } = null!;

        public string UserId { get; set; } = null!;
        public string UserFullName { get; set; } = null!;

        public CurrencyCode Currency { get; set; }
        public decimal Balance { get; set; }

        public AccountStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
