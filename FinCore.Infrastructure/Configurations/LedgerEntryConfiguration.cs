using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure.Configurations
{
    public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
    {
        public void Configure(EntityTypeBuilder<LedgerEntry> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.ToTable(t =>
                t.HasCheckConstraint(
                    "CK_LedgerEntries_Amount_Positive",
                    "[Amount] > 0"));

            builder.Property(x => x.Currency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

            builder.Property(x => x.Type)
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasOne(x => x.BankAccount)
            .WithMany(x => x.LedgerEntries)
            .HasForeignKey(x => x.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.LedgerTransaction)
                .WithMany(x => x.Entries)
                .HasForeignKey(x => x.LedgerTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
