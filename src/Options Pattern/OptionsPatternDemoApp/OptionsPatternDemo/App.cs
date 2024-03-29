using Microsoft.Extensions.Options;
using OptionsPatternDemo.Options;

namespace OptionsPatternDemo;
public class App
{
    private readonly AnimalOptions _options;
    public App(IOptions<AnimalOptions> options)
    {
        _options = options.Value;
    }
    public void Run()
    {
        Console.WriteLine($"The animal is called {_options.Name} and it has {_options.NumberOfLegs} leg(s).");
    }
}
