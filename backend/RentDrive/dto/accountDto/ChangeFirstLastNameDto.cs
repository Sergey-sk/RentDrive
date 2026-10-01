using FluentValidation;

namespace RentDrive.dto.authDto
{
    public record ChangeFirstLastNameDto(
        string? FirstName,
        string? LastName,
        string? PhoneNumber
    );

    public class ChangeFirstLastNameDtoValidator : AbstractValidator<ChangeFirstLastNameDto>
    {
        public ChangeFirstLastNameDtoValidator()
        {
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.FirstName) ||
                           !string.IsNullOrWhiteSpace(x.LastName) ||
                           !string.IsNullOrWhiteSpace(x.PhoneNumber))
                .WithMessage("Необходимо заполнить хотя бы одно поле: Имя или Фамилию.");

            RuleFor(x => x.FirstName)
                .MaximumLength(50).WithMessage("Имя не может быть длиннее 50 символов.")
                .When(x => !string.IsNullOrWhiteSpace(x.FirstName));

            RuleFor(x => x.LastName)
                .MaximumLength(60).WithMessage("Фамилия не может быть длиннее 60 символов.")
                .When(x => !string.IsNullOrWhiteSpace(x.LastName));

            RuleFor(x => x.LastName)
                .MaximumLength(20).WithMessage("Номер телефона не может быть длиннее 20 символов.")
                .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
        }
    }
}
