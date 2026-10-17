using FinCore.Application.Models.AuthModels;
using Swashbuckle.AspNetCore.Filters;

namespace FinCore
{
    public class Examples
    {

        public sealed record UserRegitrationDtoExample : IExamplesProvider<UserRegistrationDto>
        {
            public UserRegistrationDto GetExamples()
            {
                return new UserRegistrationDto
                {
                    FirstName = "irakli",
                    LastName = "gvish",
                    Email = "irakligvidhiani@gmail.com",
                    Password = "Rame123456789!",
                    PersonalNumber = "12345678912",
                    PhoneNumber = "+995599046540"
                    
                };
            }
        }
    }
}
