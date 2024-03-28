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

[microsoft-documentation] : https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-8
