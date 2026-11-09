using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ManagerDtos
{
    public class ManagerDashboardDto
    {
        public int TotalUsers { get; set; }

        public int TotalAccounts { get; set; }

        public int ActiveAccounts { get; set; }

        public int FrozenAccounts { get; set; }

        public int ClosedAccounts { get; set; }

        public int TotalTransfers { get; set; }

        public int CompletedTransfers { get; set; }

        public int PendingTransfers { get; set; }

        public int FailedTransfers { get; set; }

        public int TotalConversions { get; set; }

        public int TodayTransfers { get; set; }

        public int TodayConversions { get; set; }
    }
}
