using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.AuthModels
{
    public class LoginRequestDto
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
