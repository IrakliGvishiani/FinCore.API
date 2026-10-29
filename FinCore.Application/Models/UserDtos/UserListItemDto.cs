using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.UserDtos
{
    public class UserListItemDto
    {
        public string Id { get; set; } = null!;

        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string PersonalNumber { get; set; } = null!;

        public string? PhoneNumber { get; set; }

        public string? ProfilePhotoUrl { get; set; }

        public DateTime CreatedAt { get; set; }

        public IList<string> Roles { get; set; } = new List<string>();
    }
}
