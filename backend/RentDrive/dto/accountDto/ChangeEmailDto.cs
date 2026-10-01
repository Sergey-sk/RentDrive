using FluentValidation;

namespace RentDrive.dto.accountDto
{
    public record ChangeEmailDto(
        string Email,
        string CurrentPassword
    );

    public class ChangeEmailDtoValidator : AbstractValidator<ChangeEmailDto>
    {
        public ChangeEmailDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email обязателен для заполнения.")
                .EmailAddress().WithMessage("Некорректный формат Email-адреса.")
                .MaximumLength(256).WithMessage("Email слишком длинный.");

            RuleFor(x => x.CurrentPassword)
                .NotEmpty().WithMessage("Текущий пароль обязателен для подтверждения операции.");
        }
    }
}
