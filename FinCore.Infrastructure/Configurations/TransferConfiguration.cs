using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure.Configurations
{
    public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
    {
        public void Configure(EntityTypeBuilder<Transfer> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.ToTable("Transfers", table =>
            {
                table.HasCheckConstraint(
                    "CK_Transfers_Amount_Positive",
                    "[Amount] > 0");
                table.HasCheckConstraint(
       "CK_Transfers_DifferentAccounts",
       "[FromAccountId] <> [ToAccountId]");

            });

            builder.Property(x => x.Currency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(500);

            builder.Property(x => x.IdempotencyKey)
                .IsRequired()
                .HasMaxLength(100);
            builder.Property(t => t.InitiatedByUserId)
            .IsRequired()
            .HasMaxLength(450);


            builder.HasIndex(t => new { t.InitiatedByUserId, t.IdempotencyKey })
            .IsUnique();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.CompletedAt)
    .IsRequired(false);

            builder.Property(x => x.FailureReason)
    .HasMaxLength(500)
    .IsRequired(false);

            builder.HasOne(x => x.FromAccount)
                .WithMany(x => x.OutgoingTransfers)
                .HasForeignKey(x => x.FromAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ToAccount)
                .WithMany(x => x.IncomingTransfers)
                .HasForeignKey(x => x.ToAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
