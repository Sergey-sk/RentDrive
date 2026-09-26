using FluentValidation;

namespace RentDrive.dto.authDto
{
    public record EditProfileDto(
        string? Email,
        string? FirstName,
        string? LastName,
        string? CurrentPassword,
        string? NewPassword
    );
}
