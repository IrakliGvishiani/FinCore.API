using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace FinCore.Domain.Entities
{
    public class SavedRecipient
    {
        private SavedRecipient() { }

        public int Id { get; private set; }

        public string UserId { get; private set; } = null!;
        public ApplicationUser User { get; private set; } = null!;

        public string Name { get; private set; } = null!;

        public string AccountNumber { get; private set; } = null!;

        public DateTime CreatedAt { get; private set; }

        public static SavedRecipient Create(
            string userId,
            string name,
            string accountNumber
            )
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID is required.");

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Recipient name is required.");

            if (string.IsNullOrWhiteSpace(accountNumber))
                throw new ArgumentException("Account number is required.");

            return new SavedRecipient
            {
                UserId = userId,
                Name = name,
                AccountNumber = accountNumber,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void UpdateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Recipient name is required.");
            Name = name.Trim();
        }

    }
}
