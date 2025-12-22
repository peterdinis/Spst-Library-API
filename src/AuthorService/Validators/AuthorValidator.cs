using FluentValidation;
using AuthorService.Dtos;
using AuthorService.Entities;

namespace AuthorService.Validators
{
    public class AuthorValidator : AbstractValidator<Author>
    {
        public AuthorValidator()
        {
            RuleFor(author => author.FirstName)
                .NotEmpty().WithMessage("First name is required")
                .Length(2, 50).WithMessage("First name must be between 2 and 50 characters")
                .Matches(@"^[a-zA-ZáčďéěíňóřšťůúýžÁČĎÉĚÍŇÓŘŠŤŮÚÝŽ\s\-']+$")
                .WithMessage("First name can only contain letters, spaces, hyphens and apostrophes");

            RuleFor(author => author.LastName)
                .NotEmpty().WithMessage("Last name is required")
                .Length(2, 50).WithMessage("Last name must be between 2 and 50 characters")
                .Matches(@"^[a-zA-ZáčďéěíňóřšťůúýžÁČĎÉĚÍŇÓŘŠŤŮÚÝŽ\s\-']+$")
                .WithMessage("Last name can only contain letters, spaces, hyphens and apostrophes");

            RuleFor(author => author.Biography)
                .MaximumLength(500).WithMessage("Biography cannot exceed 500 characters")
                .When(author => !string.IsNullOrEmpty(author.Biography));

            RuleFor(author => author.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Valid email address is required")
                .MaximumLength(100).WithMessage("Email cannot exceed 100 characters");

            RuleFor(author => author.DateOfBirth)
                .LessThan(DateTime.Today).WithMessage("Date of birth must be in the past")
                .When(author => author.DateOfBirth.HasValue);

            RuleFor(author => author.DateOfDeath)
                .GreaterThan(author => author.DateOfBirth).WithMessage("Date of death must be after date of birth")
                .LessThanOrEqualTo(DateTime.Today).WithMessage("Date of death cannot be in the future")
                .When(author => author.DateOfBirth.HasValue && author.DateOfDeath.HasValue);

            RuleFor(author => author.Country)
                .NotEmpty().WithMessage("Country is required")
                .Length(2, 50).WithMessage("Country must be between 2 and 50 characters")
                .Matches(@"^[a-zA-ZáčďéěíňóřšťůúýžÁČĎÉĚÍŇÓŘŠŤŮÚÝŽ\s\-()]+$")
                .WithMessage("Country can only contain letters, spaces, hyphens and parentheses");

            RuleFor(author => author.Website)
                .MaximumLength(200).WithMessage("Website URL cannot exceed 200 characters")
                .Matches(@"^(https?://)?([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?$")
                .WithMessage("Valid website URL is required")
                .When(author => !string.IsNullOrEmpty(author.Website));
        }
    }

    public class CreateAuthorDtoValidator : AbstractValidator<CreateAuthorDto>
    {
        public CreateAuthorDtoValidator()
        {
            RuleFor(dto => dto.FirstName)
                .NotEmpty().WithMessage("First name is required")
                .Length(2, 50).WithMessage("First name must be between 2 and 50 characters")
                .Matches(@"^[a-zA-ZáčďéěíňóřšťůúýžÁČĎÉĚÍŇÓŘŠŤŮÚÝŽ\s\-']+$")
                .WithMessage("First name can only contain letters, spaces, hyphens and apostrophes");

            RuleFor(dto => dto.LastName)
                .NotEmpty().WithMessage("Last name is required")
                .Length(2, 50).WithMessage("Last name must be between 2 and 50 characters")
                .Matches(@"^[a-zA-ZáčďéěíňóřšťůúýžÁČĎÉĚÍŇÓŘŠŤŮÚÝŽ\s\-']+$")
                .WithMessage("Last name can only contain letters, spaces, hyphens and apostrophes");

            RuleFor(dto => dto.Biography)
                .MaximumLength(500).WithMessage("Biography cannot exceed 500 characters")
                .When(dto => !string.IsNullOrEmpty(dto.Biography));

            RuleFor(dto => dto.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Valid email address is required")
                .MaximumLength(100).WithMessage("Email cannot exceed 100 characters");

            RuleFor(dto => dto.DateOfBirth)
                .LessThan(DateTime.Today).WithMessage("Date of birth must be in the past")
                .When(dto => dto.DateOfBirth.HasValue);

            RuleFor(dto => dto.DateOfDeath)
                .GreaterThan(dto => dto.DateOfBirth).WithMessage("Date of death must be after date of birth")
                .LessThanOrEqualTo(DateTime.Today).WithMessage("Date of death cannot be in the future")
                .When(dto => dto.DateOfBirth.HasValue && dto.DateOfDeath.HasValue);

            RuleFor(dto => dto.Country)
                .NotEmpty().WithMessage("Country is required")
                .Length(2, 50).WithMessage("Country must be between 2 and 50 characters")
                .Matches(@"^[a-zA-ZáčďéěíňóřšťůúýžÁČĎÉĚÍŇÓŘŠŤŮÚÝŽ\s\-()]+$")
                .WithMessage("Country can only contain letters, spaces, hyphens and parentheses");

            RuleFor(dto => dto.Website)
                .MaximumLength(200).WithMessage("Website URL cannot exceed 200 characters")
                .Matches(@"^(https?://)?([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?$")
                .WithMessage("Valid website URL is required")
                .When(dto => !string.IsNullOrEmpty(dto.Website));
        }
    }

    public class UpdateAuthorDtoValidator : AbstractValidator<UpdateAuthorDto>
    {
        public UpdateAuthorDtoValidator()
        {
            RuleFor(dto => dto.AuthorId)
                .GreaterThan(0).WithMessage("Author ID must be greater than 0");

            RuleFor(dto => dto.FirstName)
                .NotEmpty().WithMessage("First name is required")
                .Length(2, 50).WithMessage("First name must be between 2 and 50 characters")
                .Matches(@"^[a-zA-ZáčďéěíňóřšťůúýžÁČĎÉĚÍŇÓŘŠŤŮÚÝŽ\s\-']+$")
                .WithMessage("First name can only contain letters, spaces, hyphens and apostrophes");

            RuleFor(dto => dto.LastName)
                .NotEmpty().WithMessage("Last name is required")
                .Length(2, 50).WithMessage("Last name must be between 2 and 50 characters")
                .Matches(@"^[a-zA-ZáčďéěíňóřšťůúýžÁČĎÉĚÍŇÓŘŠŤŮÚÝŽ\s\-']+$")
                .WithMessage("Last name can only contain letters, spaces, hyphens and apostrophes");

            RuleFor(dto => dto.Biography)
                .MaximumLength(500).WithMessage("Biography cannot exceed 500 characters")
                .When(dto => !string.IsNullOrEmpty(dto.Biography));

            RuleFor(dto => dto.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Valid email address is required")
                .MaximumLength(100).WithMessage("Email cannot exceed 100 characters");

            RuleFor(dto => dto.DateOfBirth)
                .LessThan(DateTime.Today).WithMessage("Date of birth must be in the past")
                .When(dto => dto.DateOfBirth.HasValue);

            RuleFor(dto => dto.DateOfDeath)
                .GreaterThan(dto => dto.DateOfBirth).WithMessage("Date of death must be after date of birth")
                .LessThanOrEqualTo(DateTime.Today).WithMessage("Date of death cannot be in the future")
                .When(dto => dto.DateOfBirth.HasValue && dto.DateOfDeath.HasValue);

            RuleFor(dto => dto.Country)
                .NotEmpty().WithMessage("Country is required")
                .Length(2, 50).WithMessage("Country must be between 2 and 50 characters")
                .Matches(@"^[a-zA-ZáčďéěíňóřšťůúýžÁČĎÉĚÍŇÓŘŠŤŮÚÝŽ\s\-()]+$")
                .WithMessage("Country can only contain letters, spaces, hyphens and parentheses");

            RuleFor(dto => dto.Website)
                .MaximumLength(200).WithMessage("Website URL cannot exceed 200 characters")
                .Matches(@"^(https?://)?([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?$")
                .WithMessage("Valid website URL is required")
                .When(dto => !string.IsNullOrEmpty(dto.Website));
        }
    }
}