using FluentValidation;
using BooksService.Dtos;

namespace BooksService.Validators
{
    public class UpdateBookDtoValidator : AbstractValidator<UpdateBookDto>
    {
        public UpdateBookDtoValidator()
        {

            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required")
                .MaximumLength(200).WithMessage("Title cannot exceed 200 characters");

            RuleFor(x => x.AuthorId)
                .GreaterThan(0).WithMessage("Author ID must be greater than 0");

            RuleFor(x => x.Publisher)
                .MaximumLength(100).WithMessage("Publisher cannot exceed 100 characters");

            RuleFor(x => x.Year)
                .InclusiveBetween(1000, DateTime.Now.Year).WithMessage("Year must be valid");

            RuleFor(x => x.ISBN)
                .NotEmpty().WithMessage("ISBN is required")
                .MaximumLength(20).WithMessage("ISBN cannot exceed 20 characters");

            RuleFor(x => x.Pages)
                .GreaterThan(0).WithMessage("Pages must be greater than 0")
                .LessThanOrEqualTo(10000).WithMessage("Pages cannot exceed 10000");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("Category ID must be greater than 0");

            RuleFor(x => x.Language)
                .MaximumLength(50).WithMessage("Language cannot exceed 50 characters");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters");
        }
    }
}