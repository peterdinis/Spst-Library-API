using BooksService.Dtos;
using FluentValidation;

namespace BooksService.Validators
{
    public class BookValidator : AbstractValidator<CreateBookDto>
    {
        public BookValidator()
        {
            RuleFor(b => b.Title)
                .NotEmpty().WithMessage("Title is required")
                .MaximumLength(200).WithMessage("Title must be less than 200 characters");

            RuleFor(b => b.Author)
                .NotEmpty().WithMessage("Author is required")
                .MaximumLength(100);

            RuleFor(b => b.Publisher)
                .NotEmpty().WithMessage("Publisher is required");

            RuleFor(b => b.Year)
                .InclusiveBetween(1000, DateTime.Now.Year)
                .WithMessage("Year must be valid");

            RuleFor(b => b.ISBN)
                .NotEmpty().WithMessage("ISBN is required")
                .Length(10, 13).WithMessage("ISBN must be 10 or 13 characters");

            RuleFor(b => b.Pages)
                .GreaterThan(0).WithMessage("Pages must be greater than 0");

            RuleFor(b => b.Category)
                .NotEmpty();

            RuleFor(b => b.Language)
                .NotEmpty();

            RuleFor(b => b.Description)
                .MaximumLength(1000);

            RuleFor(b => b.PhotoPath)
                .NotEmpty().WithMessage("PhotoPath is required")
                .Must(uri => Uri.IsWellFormedUriString(uri, UriKind.Absolute))
                .WithMessage("PhotoPath must be a valid URL");
        }
    }
}
