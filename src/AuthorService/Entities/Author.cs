using System.ComponentModel.DataAnnotations;

namespace AuthorService.Entities
{
    public class Author
    {
        [Key]
        public int AuthorId { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = null!;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = null!;

        [StringLength(500, ErrorMessage = "Biography cannot exceed 500 characters")]
        [DataType(DataType.MultilineText)]
        public string Biography { get; set; } = null!;

        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = null!;

        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Date of Death")]
        [DataType(DataType.Date)]
        public DateTime? DateOfDeath { get; set; }

        [Display(Name = "Country")]
        [StringLength(50)]
        public string Country { get; set; } = null!;

        [Display(Name = "Website")]
        [Url(ErrorMessage = "Invalid URL format")]
        public string Website { get; set; } = null!;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Last Modified")]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;


        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";

        public int? Age
        {
            get
            {
                if (!DateOfBirth.HasValue) return null;
                
                var endDate = DateOfDeath ?? DateTime.Now;
                var age = endDate.Year - DateOfBirth.Value.Year;
                
                if (DateOfBirth.Value.Date > endDate.AddYears(-age)) 
                    age--;
                
                return age;
            }
        }


        [Display(Name = "Status")]
        public string Status => DateOfDeath.HasValue ? "Deceased" : "Alive";
    }
}