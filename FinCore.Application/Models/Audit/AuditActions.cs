using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace FinCore.Application.Models.Audit
{
    public static class AuditActions
    {
        public const string LoginSucceeded = "auth.login.succeeded";
        public const string LoginFailed = "auth.login.failed";
        public const string Registered = "auth.registered";
        public const string PasswordResetRequested = "auth.password.reset_requested";
        public const string PasswordResetCompleted = "auth.password.reset_completed";
        public const string AccountOpened = "account.opened";
        public const string AccountClosed = "account.closed";
        public const string AccountFrozen = "account.frozen";
        public const string AccountUnfrozen = "account.unfrozen";
        public const string Deposit = "account.deposit";
        public const string TransferCompleted = "transfer.completed";
        public const string ConversionCompleted = "conversion.completed";
        public const string RoleChanged = "admin.role_changed";

        public static readonly IReadOnlyList<string> All = typeof(AuditActions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .OrderBy(x => x)
        .ToList();
    }

    public static class AuditEntities
    {
        public const string User = "User";
        public const string BankAccount = "BankAccount";
        public const string Transfer = "Transfer";
        public const string Conversion = "CurrencyConversion";
    }

    
}
