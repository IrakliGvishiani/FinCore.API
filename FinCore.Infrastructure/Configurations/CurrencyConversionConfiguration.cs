using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure.Configurations
{
    public class CurrencyConversionConfiguration : IEntityTypeConfiguration<CurrencyConversion>
    {
        public void Configure(EntityTypeBuilder<CurrencyConversion> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.FromCurrency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

            builder.Property(x => x.ToCurrency)
           .HasConversion<string>()
           .HasMaxLength(3)
           .IsRequired();

            builder.Property(x => x.Reference)
               .IsRequired()
               .HasMaxLength(100);

            builder.HasIndex(x => x.Reference)
                .IsUnique();

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

            builder.Property(x => x.Rate)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(x => x.FromAmount)
                .HasPrecision(18, 2)
                .IsRequired();
            builder.Property(x => x.ToAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            //builder.HasOne<BankAccount>()
            //.WithMany()
            //.HasForeignKey(x => x.FromAccountId)
            //.OnDelete(DeleteBehavior.Restrict);

            //builder.HasOne<BankAccount>()
            //    .WithMany()
            //    .HasForeignKey(x => x.ToAccountId)
            //    .OnDelete(DeleteBehavior.Restrict);
            //builder.ToTable(t =>
            //{
            //    t.HasCheckConstraint("CK_CurrencyConversions_FromAmount_Positive", "[FromAmount] > 0");
            //    t.HasCheckConstraint("CK_CurrencyConversions_ToAmount_Positive", "[ToAmount] > 0");
            //    t.HasCheckConstraint("CK_CurrencyConversions_Rate_Positive", "[Rate] > 0");
            //    t.HasCheckConstraint("CK_CurrencyConversions_DifferentAccounts", "[FromAccountId] <> [ToAccountId]");
            //    t.HasCheckConstraint("CK_CurrencyConversions_DifferentCurrencies", "[FromCurrency] <> [ToCurrency]");
            //});

            builder.HasOne(x => x.FromAccount).WithMany()
               .HasForeignKey(x => x.FromAccountId).OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ToAccount).WithMany()
                .HasForeignKey(x => x.ToAccountId).OnDelete(DeleteBehavior.Restrict);


            builder.Property(x => x.FeeRate).HasPrecision(9, 6).IsRequired();
            builder.Property(x => x.FeeAmount).HasPrecision(18, 2).IsRequired();
        }
    }
}
