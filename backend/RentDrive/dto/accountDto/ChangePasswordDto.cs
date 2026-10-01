using FluentValidation;

namespace RentDrive.dto.accountDto
{
    public record ChangePasswordDto(
        string NewPassword,
        string CurrentPassword
    );

    public class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
    {
        public ChangePasswordDtoValidator()
        {
            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("Новый пароль не может быть пустым.")
                .MinimumLength(4).WithMessage("Пароль должен быть не менее 4 символов.")
                .MaximumLength(100).WithMessage("Пароль не может быть длиннее 100 символов.");
            RuleFor(x => x.CurrentPassword)
                .NotEmpty().WithMessage("Текущий пароль не может быть пустым.");
        }
    }
}
