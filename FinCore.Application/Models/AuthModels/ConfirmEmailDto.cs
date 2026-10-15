using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.AuthModels
{
    public class ConfirmEmailDto
    {
        public string Email { get; set; }

        public string Code { get; set; }
    }
}
