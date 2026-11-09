using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Entities
{
    public class SimulatedPayment
    {
        private SimulatedPayment() { }

        public int Id { get; private set; }
        public int VirtualCardId { get; private set; }
        public decimal Amount { get; private set; }
        public CurrencyCode Currency { get; private set; }
        public SimulatedPaymentStatus Status { get; private set; }
        public PaymentDeclineReason? FailureReason { get; private set; }
        public string Merchant { get; private set; } = null!;
        public string? Description { get; private set; }
        public string Reference { get; private set; } = null!;
        public string IdempotencyKey { get; private set; } = null!;
        public DateTime CreatedAt { get; private set; }

        public static SimulatedPayment Create(
            VirtualCard card,CurrencyCode currency,decimal amount,
            string merchant,string? description,string idempotencyKey,
            DateTime utcNow
            )
        {
            if (string.IsNullOrWhiteSpace(merchant) || merchant.Trim().Length > 100)
                throw new DomainException("Merchant is required (max 100 characters).");

            if (card is null)
                throw new DomainException("Virtual card is required.");

            if (amount <= 0 || decimal.Round(amount, 2) != amount)
                throw new DomainException("Invalid payment amount.");

            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new DomainException("Idempotency key is required.");
            if (idempotencyKey.Length > 100)
                throw new DomainException("Idempotency key is too long.");


            return new SimulatedPayment
            {
                VirtualCardId = card.Id,
                Amount = amount,
                Currency = currency,
                Status = SimulatedPaymentStatus.Pending,
                FailureReason = null,
                Merchant = merchant.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Reference = $"PAY-{Guid.NewGuid():N}",
                IdempotencyKey = idempotencyKey.Trim(),
                CreatedAt = utcNow
            };
        }

        public void Complete()
        {
            if (Status != SimulatedPaymentStatus.Pending)
                throw new DomainException("Only pending payments can be completed.");

            Status = SimulatedPaymentStatus.Completed;
        }

        public void Fail(PaymentDeclineReason reason)
        {
            if (Status != SimulatedPaymentStatus.Pending)
                throw new DomainException("Only pending payments can be failed.");

            Status = SimulatedPaymentStatus.Failed;
            FailureReason = reason;
        }

    }
}
