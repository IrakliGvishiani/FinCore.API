using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure.Configurations
{
    public class CurrencyExchangeRateConfiguration : IEntityTypeConfiguration<CurrencyExchangeRate>
    {
        public void Configure(EntityTypeBuilder<CurrencyExchangeRate> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.FromCurrency)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(3);

            builder.Property(x => x.ToCurrency)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(3);

            builder.Property(x => x.Rate)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.ToTable("CurrencyExchangeRates", table =>
            {
                table.HasCheckConstraint(
                    "CK_CurrencyExchangeRates_Rate_Positive",
                    "[Rate] > 0");

                table.HasCheckConstraint(
                    "CK_CurrencyExchangeRates_DifferentCurrencies",
                    "[FromCurrency] <> [ToCurrency]");
            });

            builder.Property(x => x.EffectiveAt)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasIndex(x => new
            {
                x.FromCurrency,
                x.ToCurrency,
                x.EffectiveAt
            }).IsUnique();
        }
    }
}
