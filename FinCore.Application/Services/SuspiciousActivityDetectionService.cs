using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.SuspiciousActivity;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class SuspiciousActivityDetectionService : ISuspiciousActivityDetectionService
    {
        private readonly IRepositoryBase<Transfer> _transferRepository;
        private readonly IRepositoryBase<SuspiciousActivity> _suspiciousActivityRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOptions<SuspiciousActivityOptions> _suspiciousActivityOptions;


        public SuspiciousActivityDetectionService(
            IRepositoryBase<Transfer> transferRepository,
            IRepositoryBase<SuspiciousActivity> suspiciousActivityRepository,
            IUnitOfWork unitOfWork,
            IOptions<SuspiciousActivityOptions> suspiciousActivityOptions)
        {
            _transferRepository = transferRepository;
            _suspiciousActivityRepository = suspiciousActivityRepository;
            _unitOfWork = unitOfWork;
            _suspiciousActivityOptions = suspiciousActivityOptions;
        }

        public async Task CheckTransferAsync(int transferId, decimal balanceBeforeTransfer, 
            CancellationToken ct = default)
        {

            var transfer = await _transferRepository.GetAsync(
                x => x.Id == transferId,
                tracking: false,
                cancellationToken: ct
                );

            if (transfer == null)
                throw new NotFoundException("Transfer not found");

            if (transfer.Status != TransferStatus.Completed)
                return;

            var alreadyDetected = await _suspiciousActivityRepository.ExistsAsync(
                x => x.TransferId == transferId, ct
                );

            if (alreadyDetected)
                return;

            var options = _suspiciousActivityOptions.Value;
            var reasons = new List<string>();
            var riskScore = 0;
            var riskOptions = options.RiskScore;

            // Rule 1: Rapid Transfers: If a user makes more than a certain number of transfers within a short time frame, it may indicate suspicious activity.

            var timeWindowMinutes = options.RapidTransfers.TimeWindowMinutes;
            var minimumTransferCount = options.RapidTransfers.MinimumTransferCount;

            var fromDate = transfer.CreatedAt.AddMinutes(-timeWindowMinutes);

            var transferCount = await _transferRepository.CountAsync(
                x =>
                x.InitiatedByUserId == transfer.InitiatedByUserId &&
                x.Status == TransferStatus.Completed &&
                x.CreatedAt >= fromDate &&
                x.CreatedAt <= transfer.CreatedAt,
                ct
                );

            if (transferCount >= minimumTransferCount)
            {
                reasons.Add(
             $"User completed {transferCount} transfers within {timeWindowMinutes} minutes.");

                riskScore += riskOptions.RapidTransfers;
            }

            // Rule 2: Large Transfers: If a transfer amount exceeds a certain threshold, it may be flagged as suspicious.
            var currency = transfer.Currency.ToString();

            if (options.LargeTransferThresholds.TryGetValue(currency, out var threshold) &&
                transfer.Amount >= threshold)
            {
                reasons.Add(
             $"User transferred {transfer.Amount} {currency}, which exceeds the threshold of {threshold}.");

                riskScore += riskOptions.LargeTransfer;
            }

            // Rule 3 : Multiple Recipients: If a user sends transfers to multiple different recipients in a short time frame, it may indicate suspicious activity.

            var recipientOptions = options.MultipleRecipients;

            var recipientFromDate = transfer.CreatedAt.AddMinutes(-recipientOptions.TimeWindowMinutes);


            var recipientCount = await _transferRepository.CountDistinctAsync(
                x =>
                x.InitiatedByUserId == transfer.InitiatedByUserId &&
                x.Status == TransferStatus.Completed &&
                x.CreatedAt >= recipientFromDate &&
                x.CreatedAt <= transfer.CreatedAt,
                x => x.ToAccountId,
                ct
                );

            if (recipientCount >= recipientOptions.MinimumRecipientCount)
            {
                reasons.Add(
        $"User transferred money to {recipientCount} different recipients " +
        $"within {recipientOptions.TimeWindowMinutes} minutes."
    );

                riskScore += riskOptions.MultipleRecipients;
            }


            // Rule 4: Unusual Transfer Amounts: If a transfer amount is significantly higher than the user's typical transfer amounts, it may be flagged as suspicious.

            var unusualOptions = options.UnusualTransfers;

            var lookBackDate = transfer.CreatedAt.AddDays(-unusualOptions.LookBackDays);


            var previousTransfers = await _transferRepository.GetAllAsync(
                filter: x =>
                    x.InitiatedByUserId == transfer.InitiatedByUserId &&
                    x.Status == TransferStatus.Completed &&
                    x.Currency == transfer.Currency &&
                    x.CreatedAt >= lookBackDate &&
                    x.CreatedAt < transfer.CreatedAt,
                tracking: false,
                cancellationToken: ct
            );

            var previousAmounts = previousTransfers.Items
                .Select(x => x.Amount)
                .ToList();

            if(previousAmounts.Count >= unusualOptions.MinimumTransferCount)
            {
                var averageAmount = previousAmounts.Average();

                if (transfer.Amount >= averageAmount * unusualOptions.AverageAmountMultiplier)
                {
                    reasons.Add(
                    $"Unusual transfer amount: {transfer.Amount} {transfer.Currency}, " +
                    $"compared to user's average of {averageAmount:F2} {transfer.Currency}."
                     );

                    riskScore += riskOptions.UnusualTransferAmount;
                }
            }

            // Rule 5: Account Draining

            var drainingOptions = options.AccountDraining;

            if(transfer.Amount >= drainingOptions.MinimumTransferAmount &&
                balanceBeforeTransfer > 0)
            {
                var balancePercentage =
                transfer.Amount / balanceBeforeTransfer * 100;

                if (balancePercentage >= drainingOptions.BalancePercentageThreshold)
                {
                    reasons.Add(
                      $"Account draining detected: {balancePercentage:F2}% of the original balance was transferred."
);

                    riskScore += riskOptions.AccountDraining;
                }
            }

            if (reasons.Count == 0)
                return;

            var activity = SuspiciousActivity.Create(
                transfer.InitiatedByUserId,
                transfer.Id,
                string.Join(" ", reasons),
                riskScore

                );

            await _suspiciousActivityRepository.AddAsync(activity, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}
