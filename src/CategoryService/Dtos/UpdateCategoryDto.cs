using System.ComponentModel.DataAnnotations;


namespace CategoryService.Dtos;

public class UpdateCategoryDto
{
    [Required(ErrorMessage = "Názov kategórie je povinný")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Názov musí mať medzi 2 a 100 znakmi")]
    public string Title { get; set; } = null!;

    [Required(ErrorMessage = "Popis kategórie je povinný")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Popis musí mať medzi 10 a 1000 znakmi")]
    public string Description { get; set; } = null!;
}