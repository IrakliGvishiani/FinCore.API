using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.SavedRecipientDtos
{
    public class CreateSavedRecipientDto
    {
        public string Name { get; set; } = null!;
        public string AccountNumber { get; set; } = null!;
    }
}
