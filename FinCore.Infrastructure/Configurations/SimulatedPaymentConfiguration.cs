using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure.Configurations
{
    public class SimulatedPaymentConfiguration : IEntityTypeConfiguration<SimulatedPayment>
    {
        public void Configure(EntityTypeBuilder<SimulatedPayment> builder)
        {
            builder.ToTable("SimulatedPayments");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.Currency)
                .HasConversion<string>()
                .HasMaxLength(3)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.FailureReason)
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.Property(x => x.Merchant)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Description)
                .HasMaxLength(500);

            builder.Property(x => x.Reference)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.IdempotencyKey)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.ChargedCurrency)
                .HasConversion<string>()
                .HasMaxLength(3)
                .IsRequired();

            // Nullable: only set once the payment is priced (declines before pricing leave them null).
            builder.Property(x => x.ChargedAmount)
                .HasPrecision(18, 2);

            builder.Property(x => x.ExchangeRate)
                .HasPrecision(18, 8);

            builder.Property(x => x.FeeAmount)
                .HasPrecision(18, 2);

            builder.Property(x => x.LimitAmount)
                .HasPrecision(18, 2);

            builder.Property(x => x.RateDate);

            builder.Ignore(x => x.IsPriced);
            builder.Ignore(x => x.IsConversion);

            builder.HasOne<VirtualCard>()
                .WithMany()
                .HasForeignKey(x => x.VirtualCardId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<BankAccount>()
                .WithMany()
                .HasForeignKey(x => x.SourceBankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => x.Reference)
                .IsUnique();

            builder.HasIndex(x => new { x.VirtualCardId, x.IdempotencyKey })
                .IsUnique();

            builder.HasIndex(x => new { x.VirtualCardId, x.CreatedAt });
        }
    }
}
