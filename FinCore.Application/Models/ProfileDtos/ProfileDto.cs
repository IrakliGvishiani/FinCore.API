using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ProfileDtos
{
    public class ProfileDto
    {
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string MaskedPersonalNumber { get; set; } = null!;
        public string? ProfilePhotoUrl { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
        public DateTime CreatedAt { get; set; }
    }
}
