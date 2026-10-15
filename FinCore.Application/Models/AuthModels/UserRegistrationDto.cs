using System.ComponentModel.DataAnnotations;

namespace FinCore.Application.Models.AuthModels;

public class UserRegistrationDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{11}$",
        ErrorMessage = "Personal number must contain exactly 11 digits.")]
    public string PersonalNumber { get; set; } = string.Empty;

    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [MinLength(10)]
    public string Password { get; set; } = string.Empty;
}