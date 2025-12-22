using FluentValidation;
using CategoryService.Entities;

namespace CategoryService.Validators
{
    public class CategoryValidator : AbstractValidator<Category>
    {
        public CategoryValidator()
        {
            RuleFor(category => category.Title)
                .NotEmpty().WithMessage("Názov kategórie je povinný")
                .Length(2, 100).WithMessage("Názov kategórie musí mať medzi 2 a 100 znakmi")
                .Matches(@"^[a-zA-Z0-9áčďéěíňóřšťůúýžÁČĎÉĚÍŇÓŘŠŤŮÚÝŽ\s\-_\.]+$")
                .WithMessage("Názov kategórie môže obsahovať iba písmená, čísla, medzery a znaky -_.");

            RuleFor(category => category.Description)
                .NotEmpty().WithMessage("Popis kategórie je povinný")
                .Length(10, 500).WithMessage("Popis kategórie musí mať medzi 10 a 500 znakmi")
                .MaximumLength(1000).WithMessage("Popis kategórie nesmie byť dlhší ako 1000 znakov");

            RuleFor(category => category.Id)
                .Equal(0).When(category => category.Id == 0)
                .WithMessage("ID musí byť 0 pre novú kategóriu");
        }
    }
}