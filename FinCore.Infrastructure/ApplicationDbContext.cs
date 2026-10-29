using FinCore.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }


        override protected void OnModelCreating(ModelBuilder modelBuilder)
        {

            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            modelBuilder.RenameIdentityTables();
        }


        public DbSet<BankAccount> BankAccounts { get; set; }
        public DbSet<LedgerTransaction> LedgerTransactions { get; set; }
        public DbSet<LedgerEntry> LedgerEntries { get; set; }

        public DbSet<Transfer> Transfers { get; set; }

        public DbSet<ApplicationUser> ApplicationUsers { get; set; }

        public DbSet<AuditLog> AuditLogs { get; set; }

        public DbSet<CurrencyExchangeRate> CurrencyExchangeRates { get; set; }

        public DbSet<CurrencyConversion> CurrencyConversions { get; set; }

        public DbSet<LedgerAccount> LedgerAccounts { get; set; }

    }
}
