using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure.Configurations
{
    public class VirtualCardConfiguration : IEntityTypeConfiguration<VirtualCard>
    {
        public void Configure(EntityTypeBuilder<VirtualCard> builder)
        {
            builder.ToTable("VirtualCards");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserId)
                .IsRequired()
                .HasMaxLength(450);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.Last4Digits)
                .IsRequired()
                .HasMaxLength(4)
                .IsFixedLength();

            builder.Property(x => x.ExpiryMonth)
                .IsRequired();

            builder.Property(x => x.ExpiryYear)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.DailyLimit)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.TransactionLimit)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasOne(x => x.BankAccount)
                .WithMany()
                .HasForeignKey(x => x.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => x.UserId);

            builder.HasIndex(x => x.BankAccountId);
        }
    }
}
