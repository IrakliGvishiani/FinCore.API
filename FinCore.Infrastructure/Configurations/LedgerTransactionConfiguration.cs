using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure.Configurations
{
    public class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
    {
        public void Configure(EntityTypeBuilder<LedgerTransaction> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Reference)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(x => x.Reference)
                .IsUnique();

            builder.Property(x => x.Currency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasOne(x => x.Transfer)
                .WithOne()
                .HasForeignKey<LedgerTransaction>(x => x.TransferId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.Entries)
                .WithOne(x => x.LedgerTransaction)
                .HasForeignKey(x => x.LedgerTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
