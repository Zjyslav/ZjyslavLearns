using System.ComponentModel.DataAnnotations;

namespace OptionsPatternDemo.Options;
public class AnimalOptions
{
    [Required]
    [StringLength(maximumLength: 25, MinimumLength = 1)]
    public string Name { get; set; }

    [Required]
    [Range(minimum: 0, maximum: 1000)]
    public int NumberOfLegs { get; set; }

    [Required]
    public FoodPreferences FoodPreferences { get; set; }
}