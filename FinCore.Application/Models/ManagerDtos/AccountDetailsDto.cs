using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ManagerDtos
{
    public class AccountDetailsDto  : AccountMonitoringDto
    {
        public string UserEmail { get; set; } = null!;

    }
}
