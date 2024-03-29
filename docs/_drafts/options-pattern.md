---
layout: post
title: Options Pattern
---

I love using .NET's configuration system.

The fact that I can have access to values stored in various places (multiple tiers of .json files, environment variables, command line arguments and others) via an `IConfiguration` object that comes preconfigured with an `WebApplicationBuilder` or `ApplicationBuilder` that I'm using anyway for its Dependency Injection system, makes initial setup a matter of adding `appsettings.json` file and not forgetting to set it to copy to output directory (and only if I'm working with a project that doesn't already have one).

Then, whenever I need a value from my configuration file, I can inject and `IConfiguration` object in the class I need it and get the value from it.

However, the _get the value_ part is a moment when something can go wrong. When you want to use `GetValue<T>()` method (available in `Microsoft.Extensions.Configuration.Binder` NuGet package):

- You have to provide key (a path to the value).  
  I usually hardcore a string there, either directly inside the method call, or by declaring `string key = "..."` a few lines above.
- You have to provice a type for the value.  
  In most cases, primitive types like `string` or `int` work fine for me.

**What happens when the value is not found?**  
You get a default value of the given type.

**What happens when the value is found, but could not be converted to the given type?**  
`InvalidOperationException` gets thrown.

**What happenes when the value is properly obtained, but makes no sense in your context?**  
That's something you have to handle yourself.

If you don't have full trust in the configuration that will be provided, **which you shouldn't have**, you'd better wrap it in a `try/catch` and employ some sort of validation logic.

**Is using Options Pattern an anser to all of these?**  
In a way. It's certainly not the only answer. For me, exploring this alternative way of obtaining configuration values opened my eyes to some solutions that could be utilized also when using the `IConfiguration` directly.

# How do I use Options Pattern?

It won't be a deep dive into all the ways you could use the Options Pattern. Please, reference [Microsoft's documentation][microsoft-documentation] on the topic like I did, if you're not satisfied with my example.

## Create a class for your options

Let's say my `appsettings.json` looks something like this:

```
{
    "AnimalOptions" : {
        "Name" : "Moose",
        "NumberOfLegs" : 4
    }
}
```

I would create a class looking something like this:

```
public class AnimalOptions
{
    public string Name { get; set; }
    public int NumberOfLegs { get; set; }
}
```

I make sure that all the names are exactly the same in both places. Name of properties need to be the same in order for options binding to work, but the name of the class doesn't have to exactly correspond to the name of the section.  
However, if it does, you can use `nameof(AnimalOptions)` as the key to get the right section instead of a hard coded string.  
Microsoft uses a `public const string` field with the section name. I wouldn't chose such solution, but I guess that in a situation when the path to the section is complicated or configuration file structure doesn't map nicely to a class name you want to use, it works better than my solution. As always, it depends.

## Configure services

Now, in `program.cs` (or wherever services are added for Dependency Injection) I add:

```
builder.Services.Configure<AnimalOptions>(builder.Configuration.GetSection(nameof(AnimalOptions)));
```

This line of code binds section of configuration (that had to be obtained from configuration) to `AnimalOptions`.

## Inject

You can inject one of available generic interfaces with type specified to be `AnimalOptions`. You get a choice of:

- `IOptions<TOptions>`
- `IOptionsSnapshot<TOptions>`
- `IOptionsMonitor<TOptions>`

One main difference them I think you should take into account when you choose which one you need, is when they update their values.  
`IOptions` is a singleton that reads the values during applications startup and never updates them.  
`IOptionsSnapshot` is scoped and reads the values the moment it's instantiated, so at the time of the request in web apps.  
`IOptionsMonitor` is a singleton that lets you read the current values.

In my example, I chose to use `IOptions`. I inject it in the constructor of the `App` class.

```
private readonly AnimalOptions _options;
public App(IOptions<AnimalOptions> options)
{
    _options = options.Value;
}
```

Please note that `IOptions<AnimalOptions>` is just a wrapper and the `AnimalOptions` object I care about is its `Value` property.

## Validation

Options values binded this way behave similarly to the values obtained via `IConfiguration` the way I described before:

- lack of value in configuration means default value in options
- value that cannot be converted into the type of property throws an exception

Exception being thrown on type mismatch is a correct behaviour, as far as I can tell, but getting type's default value can be not exactly what you want.  
If you wanted to check if an `int` value was specified or not, when 0 is a valid value, you could change the type to `int?` that defaults to `null`, but maybe it's not something you can or want to do for one reason or another.

The bigger problem I see here, is that you need to put your validation logic when the values are used or maybe in the constructor after the options are injected.

There is another way.

First, let's add reference to `Microsoft.Extensions.Options.DataAnnotations` NuGet package.

Then, we add data annotations (`using System.ComponentModel.DataAnnotations;`) to the properties of our options class to specify our requirements.

```
public class AnimalOptions
{
    [Required]
    [StringLength(maximumLength: 25, MinimumLength = 1)]
    public string Name { get; set; }

    [Required]
    [Range(minimum: 0, maximum: 1000)]
    public int NumberOfLegs { get; set; }
}
```

The last thing we need is to actually run the validation. For that, we need to use `ValidateDataAnnotations()` extension method. Unfortunatelly, it extends `OptionsBuilder<T>`, but when we configured options using `Configure<T>()`, it returned `IServiceCollection` object, so we cannot simply chain these methods.

Luckily, there is another way of configuring options. It's a little more verbose, but we can use it in this situation.

```
builder.Services
    .AddOptions<AnimalOptions>()
    .Bind(builder.Configuration.GetSection(nameof(AnimalOptions)))
    .ValidateDataAnnotations();
```

Now, if we don't specify a required property, or any value doesn't pass validation, an `OptionsValidationException` is thrown the moment the DI system tries to inject `IOptions` the first time.

## Nested options

In my example, the structure of options is flat. When I use `IConfiguration` directly, I don't shy away from using multiple layers of sections, but I don't particularly like to do it with options.

The way to do it, is to create a separate class for a subsection and use it as a type for a property. I added `FoodPreferences` to my `AnimalOptions`

`appsettings.json`:

```
{
  "AnimalOptions": {
    "Name": "Moose",
    "NumberOfLegs": 4,
    "FoodPreferences": {
      "Plants": true,
      "Meat": false
    }
  }
}
```

`FoodPreferences.cs`:

```
public class FoodPreferences
{
    public bool Plants { get; set; }
    public bool Meat { get; set; }
}
```

`AnimalOptions.cs`

```
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
```

Food preferences are required, so validation requires all its fields be provided and the fact they are `bool` type, they must be either `true` or `false` or they cannot get converted.

However, if I add following property to `FoodPreferences`:

```
[Required]
[StringLength(maximumLength: 25, MinimumLength = 1)]
public string FavouriteFood { get; set; }
```

and then not provide a value for it in `appsettings.json`, I don't get an error. I get a null string.

You have to be careful with nesting and validation.  
If the extent of your validation is checking if a value of type that cannot be null is provided, you might be safe.  
If you need to do some check on a value, it works only for the root of your configuration section.  
There are [ways][nested-validation] to enable such validation of nested options, but they are beyond scope of this post. I'd suggest keeping it simple and sticking with default functionality until you really need to go more complex.

## Summary

Overall, I believe that Options Pattern is a good way to make your code a bit more _clean_.

It makes your configuration strongly typed, enables validation on a higher level (in DI, instead of service) and makes your service's dependencies clearer - you know exactly what section of configuration the class depends on without having to search for any uses of `IConfiguration` in the code.

I will be more open to use this pattern in the future. It adds complexity (as any pattern does), but it has its benefits (as any _good_ pattern does), so it's a trade-off, but I believe the complexity cost is low enough to seriously consider it in most situations.

[microsoft-documentation] : https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-8
[nested-validation] : https://stackoverflow.com/questions/77036980/how-to-validate-a-c-sharp-nested-options-class
