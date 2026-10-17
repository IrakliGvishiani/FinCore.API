using FinCore.Application.Models.AuthModels;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Domain.Entities;
using Mapster;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Mapping
{
    public class MappingConfiguration : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<UserRegistrationDto, ApplicationUser>()
                .Map(dest => dest.UserName, src => src.Email)
                .Map(dest => dest.NormalizedUserName, src => src.Email.ToUpper())
                .Map(dest => dest.NormalizedEmail, src => src.Email.ToUpper())
                .Map(dest => dest.Email, src => src.Email)
                .Map(dest => dest.PhoneNumber, src => src.PhoneNumber)
                .Map(dest => dest.PersonalNumber, src => src.PersonalNumber);


            config.NewConfig<BankAccount, OpenAccountResponseDto>();

            config.NewConfig<BankAccount, AccountForGettingDto>();
        }
    }
}
