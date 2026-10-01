using FluentValidation;

namespace RentDrive.dto.authDto
{
    public record EditProfileDto(
        string? Email,
        string? FirstName,
        string? LastName,
        string? PhoneNumber,
        string? CurrentPassword,
        string? NewPassword
    );
}
