using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Entities
{
    public class SuspiciousActivity
    {
        private SuspiciousActivity() { }

        public int Id { get; private set; }

        public string UserId { get; private set; } = null!;
        public ApplicationUser User { get; private set; } = null!;

        public int TransferId { get; private set; }
        public Transfer Transfer { get; private set; } = null!;

        public string Reason { get; private set; } = null!;

        public SuspiciousActivityStatus Status { get; private set; }

        public int RiskScore { get; private set; }

        public RiskLevel RiskLevel { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public string? ReviewedByUserId { get; private set; }
        public DateTime? ReviewedAt { get; private set; }


        public static SuspiciousActivity Create(
            string userId,
            int transferId,
            string reason,
            int riskScore
        )
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID is required.");

            if (transferId <= 0)
                throw new ArgumentException("Invalid transfer ID.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason is required.");

            return new SuspiciousActivity
            {
                UserId = userId,
                TransferId = transferId,
                Reason = reason,
                RiskScore = riskScore, 
                RiskLevel = CalculateRiskLevel(riskScore),
                Status = SuspiciousActivityStatus.Detected,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void StartReview(string UserId)
        {
            if (Status != SuspiciousActivityStatus.Detected)
                throw new InvalidOperationException(
                    "Only detected suspicious activities can be reviewed.");

            if (string.IsNullOrWhiteSpace(UserId))
                throw new ArgumentException(
                    "user ID is required.",
                    nameof(UserId));

            Status = SuspiciousActivityStatus.UnderReview;
            ReviewedByUserId = UserId;
            ReviewedAt = DateTime.UtcNow;
        }

        public void Resolve()
        {
            if(Status != SuspiciousActivityStatus.UnderReview)
                throw new InvalidOperationException(
                    "Only suspicious activities under review can be resolved.");

            Status = SuspiciousActivityStatus.Resolved;
        }


        private static RiskLevel CalculateRiskLevel(int score)
        {
            return score switch
            {
                < 30 => RiskLevel.Low,
                < 60 => RiskLevel.Medium,
                _ => RiskLevel.High
            };
        }
    }
}
