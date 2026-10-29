using FinCore.Application.Models.BankAccountDtos;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure.Seed
{
    public static class IdentitySeeder
    {
        public static async Task SeedRolesAsync(IServiceProvider services)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            foreach (var role in new[] { "Admin", "Customer", "Manager" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        public static async Task SeedLedgerAccountsAsync(ApplicationDbContext db)
        {
            foreach (var currency in Enum.GetValues<CurrencyCode>())
            {
                var items = new[]
                {
            (Code: LedgerAccountCodes.Cash(currency), Name: $"Bank cash ({currency})"),
            (Code: LedgerAccountCodes.Fx(currency),   Name: $"FX position ({currency})"),
            (Code: LedgerAccountCodes.Fee(currency), Name: $"Conversion fee income ({currency})")
        };

                foreach (var (code, name) in items)
                {
                    if (!await db.LedgerAccounts.AnyAsync(a => a.AccountCode == code))
                        db.LedgerAccounts.Add(LedgerAccount.Create(code, name, currency));
                }
            }

            await db.SaveChangesAsync();
        }
    }
}
