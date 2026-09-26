using FluentValidation;

namespace RentDrive.dto.authDto
{
    public record CompleteProfileDto(
        string FirstName,
        string LastName
    );

    public class CompleteProfileDtoValidator : AbstractValidator<CompleteProfileDto>
    {
        public CompleteProfileDtoValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("Имя пользователя обязатльно к заполнению");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Фамилия пользователя обязатльно к заполнению");
        }
    }
}
