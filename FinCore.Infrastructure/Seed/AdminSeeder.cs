using FinCore.Domain.Constants;
using FinCore.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace FinCore.Infrastructure.Seed
{
    public static class AdminSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var options = services.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeeder");

        
            var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count > 0)
            {
                logger.LogInformation("An admin already exists. Admin seeding skipped.");
                return;
            }

            var missing = new[]
            {
            (Name: "AdminSeed:Email", Value: options.Email),
            (Name: "AdminSeed:Password", Value: options.Password),
            (Name: "AdminSeed:PersonalNumber", Value: options.PersonalNumber),
            (Name: "AdminSeed:PhoneNumber", Value: options.PhoneNumber),
            (Name: "AdminSeed:FirstName", Value: options.FirstName),
            (Name: "AdminSeed:LastName", Value: options.LastName)
        }
            .Where(x => string.IsNullOrWhiteSpace(x.Value))
            .Select(x => x.Name)
            .ToList();

            
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"No admin exists and these settings are missing: {string.Join(", ", missing)}.");

            
            if (!Regex.IsMatch(options.PersonalNumber!, @"^\d{11}$"))
                throw new InvalidOperationException("AdminSeed:PersonalNumber must be 11 digits.");
            if (!Regex.IsMatch(options.PhoneNumber!, @"^\+995\d{9}$"))
                throw new InvalidOperationException("AdminSeed:PhoneNumber must be +995XXXXXXXXX.");

            var email = options.Email!.Trim();

            var user = new ApplicationUser
            {
                UserName = email,                 
                Email = email,
                EmailConfirmed = true,            
                PhoneNumber = options.PhoneNumber!.Trim(),
                PersonalNumber = options.PersonalNumber!.Trim(),
                FirstName = options.FirstName!.Trim(),
                LastName = options.LastName!.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            var create = await userManager.CreateAsync(user, options.Password!);
            if (!create.Succeeded)
                throw new InvalidOperationException(
                    "Admin seeding failed: " + string.Join("; ", create.Errors.Select(e => e.Description)));

            try
            {
                var roleResult = await userManager.AddToRoleAsync(user, Roles.Admin);
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException(
                        "Admin role assignment failed: " + string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }
            catch
            {
                
                await userManager.DeleteAsync(user);
                throw;
            }

            logger.LogInformation("Admin account was created.");
        }
    }

    public class AdminSeedOptions
    {
        public const string SectionName = "AdminSeed";

        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? PersonalNumber { get; set; }
        public string? PhoneNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}
