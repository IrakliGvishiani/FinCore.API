using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.SuspiciousActivity
{
    public class SuspiciousActivityOptions
    {

        public const string SectionName = "SuspiciousActivity";

        public Dictionary<string, decimal> LargeTransferThresholds { get; set; } = [];

        public RapidTransferOptions RapidTransfers { get; set; } = new ();

        public MultipleRecipientsOptions MultipleRecipients { get; set; } = new();

        public UnusualTransferOptions UnusualTransfers { get; set; } = new();

        public AccountDrainingOptions AccountDraining { get; set; } = new();

        public RiskScoreOptions RiskScore { get; set; } = new();
    }

    public class RapidTransferOptions
    {
        public int TimeWindowMinutes { get; set; }
        public int MinimumTransferCount { get; set; }
    }

    public class MultipleRecipientsOptions
    {
        public int TimeWindowMinutes { get; set; }
        public int MinimumRecipientCount { get; set; }
    }

    public class UnusualTransferOptions
    {
        public int LookBackDays { get; set; } = 30;
        public decimal AverageAmountMultiplier { get; set; } = 5;
        public int MinimumTransferCount { get; set; } = 5;
    }

    public class AccountDrainingOptions
    {
        public decimal BalancePercentageThreshold { get; set; } = 90;
        public decimal MinimumTransferAmount { get; set; } = 1000;
    }

    public class RiskScoreOptions
    {
        public int RapidTransfers { get; set; } = 20;
        public int LargeTransfer { get; set; } = 25;
        public int MultipleRecipients { get; set; } = 20;
        public int UnusualTransferAmount { get; set; } = 25;
        public int AccountDraining { get; set; } = 30;
    }
}
