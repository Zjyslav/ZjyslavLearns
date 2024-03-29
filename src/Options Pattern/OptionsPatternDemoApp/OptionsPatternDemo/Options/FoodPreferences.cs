using System.ComponentModel.DataAnnotations;

namespace OptionsPatternDemo.Options;
public class FoodPreferences
{
    public bool Plants { get; set; }
    public bool Meat { get; set; }

    [Required]
    [StringLength(maximumLength: 25, MinimumLength = 1)]
    public string FavouriteFood { get; set; }
}
