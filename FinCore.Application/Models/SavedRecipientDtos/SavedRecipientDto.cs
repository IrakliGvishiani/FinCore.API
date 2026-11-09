using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.SavedRecipientDtos
{
    public class SavedRecipientDto
    {
        public int Id { get; set; }

        
        public string Name { get; set; } = null!;

        public string AccountNumber { get; set; } = null!;

        
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? ProfilePhotoUrl { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
