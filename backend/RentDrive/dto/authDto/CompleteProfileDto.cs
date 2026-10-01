using FluentValidation;

namespace RentDrive.dto.authDto
{
    public record CompleteProfileDto(
        string FirstName,
        string LastName,
        string PhoneNumber
    );

    public class CompleteProfileDtoValidator : AbstractValidator<CompleteProfileDto>
    {
        public CompleteProfileDtoValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("Имя пользователя обязательно к заполнению");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Фамилия пользователя обязательно к заполнению");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Номер телефона пользователя обязателен к заполнению");
        }
    }
}
