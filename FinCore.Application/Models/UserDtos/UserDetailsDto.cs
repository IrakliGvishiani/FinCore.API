using FinCore.Application.Models.BankAccountDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.UserDtos
{
    public class UserDetailsDto : UserListItemDto
    {
        public IList<UserBankAccountDto> BankAccounts { get; set; }
            = new List<UserBankAccountDto>();
    }
}
