
using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure.Configurations
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserId)
                .HasMaxLength(450);

            builder.Property(x => x.Action)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.EntityName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.EntityId)
                .HasMaxLength(100);

            builder.Property(x => x.Details)
                .HasMaxLength(2000);

            builder.Property(x => x.IpAddress)
                .HasMaxLength(45);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasIndex(x => x.CreatedAt);

            builder.HasIndex(x => x.UserId);
        }
    }
}
