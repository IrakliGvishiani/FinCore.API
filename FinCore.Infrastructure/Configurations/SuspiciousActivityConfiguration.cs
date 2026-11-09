using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure.Configurations
{
    public class SuspiciousActivityConfiguration : IEntityTypeConfiguration<SuspiciousActivity>
    {
        public void Configure(EntityTypeBuilder<SuspiciousActivity> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserId)
                .IsRequired()
                .HasMaxLength(450);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Transfer)
                .WithMany()
                .HasForeignKey(x => x.TransferId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.Reason)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.ReviewedByUserId)
                .HasMaxLength(450);

            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.ReviewedAt)
                .IsRequired(false);

            builder.HasIndex(x => x.UserId);
            builder.HasIndex(x => x.TransferId)
                .IsUnique();
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => x.CreatedAt);

            builder.Property(x => x.RiskScore)
              .IsRequired();

            builder.Property(x => x.RiskLevel)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
        }
    }
}
