using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ProfileDtos
{
    public class UpdateProfileDto
    {
        public string? FirstName { get; set; } 

        public string? LastName { get; set; } 

        public string? PhoneNumber { get; set; }
    }
}
