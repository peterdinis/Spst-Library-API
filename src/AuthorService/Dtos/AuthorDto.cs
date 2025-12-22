// AuthorService/Dtos/AuthorDto.cs
using System.ComponentModel.DataAnnotations;
using AuthorService.Messages;

namespace AuthorService.Dtos
{
    // Base DTO with common properties
    public abstract class AuthorBaseDto
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = null!;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = null!;

        [StringLength(500, ErrorMessage = "Biography cannot exceed 500 characters")]
        [Display(Name = "Biography")]
        public string Biography { get; set; } = null!;

        [EmailAddress(ErrorMessage = "Invalid email address format")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = null!;

        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Date of Death")]
        [DataType(DataType.Date)]
        public DateTime? DateOfDeath { get; set; }

        [StringLength(50, ErrorMessage = "Country cannot exceed 50 characters")]
        [Display(Name = "Country")]
        public string Country { get; set; } = null!;

        [Url(ErrorMessage = "Invalid URL format")]
        [StringLength(200, ErrorMessage = "Website URL cannot exceed 200 characters")]
        [Display(Name = "Website")]
        public string Website { get; set; } = null!;
    }

    // DTO for creating a new author
    public class CreateAuthorDto : AuthorBaseDto
    {
        // Additional properties specific to creation if needed
    }

    // DTO for updating an existing author
    public class UpdateAuthorDto : AuthorBaseDto
    {
        [Required(ErrorMessage = "Author ID is required")]
        public int AuthorId { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;
    }

    // DTO for reading/displaying author information
    public class AuthorDto : AuthorBaseDto
    {
        public int AuthorId { get; set; }

        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; }

        [Display(Name = "Last Modified")]
        public DateTime LastModified { get; set; }

        // Properties for books from BookService
        public List<AuthorBookDto> Books { get; set; } = new();
        public int BooksCount { get; set; }
        public int? Age { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}