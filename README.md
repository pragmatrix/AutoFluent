# AutoFluent - A fluent API generator

[![Build status](https://ci.appveyor.com/api/projects/status/ornm2laos0d7bio1?svg=true)](https://ci.appveyor.com/project/pragmatrix/autofluent)

AutoFluent takes a .NET assembly and generates extension methods for properties, events and methods that return void, so that they can be used in a method chain.

## Examples

TBD

## Installation

### AutoFluent command line tool

TBD

### Packages

AutoFluent generated API packages will be released on NuGet. The naming convention of the generated packages is `[Name].AutoFluent`, where `[Name]` is the original API's name, assembly, or primary namespace. 

To ease the transition to a fluent programming style, AutoFluent packages will have one dependency to a very small portable class library named [Fluent.Extensions](https://github.com/pragmatrix/Fluent.Extensions) that contains all the code in [Collections.Fluent](https://github.com/pragmatrix/Collections.Fluent) and some more methods that are useful in method chains.

## Generators

Right now, AutoFluent generates extension methods for properties, events, and methods that return `void`.

### Properties

For each public property in a non-static class or interface, an extension method with the same name and the property type as its only parameter is generated. The method body assigns the property and returns the instance of the class.

A typical extension method for a simple property looks like this:

    public static Label Title(this Label self, string value)
    {
        self.Title = value;
        return self;
    }

The generator can handle generic properties in generic classes and will add type parameters and constraints as needed.

### Events

For each public event in a non-static class or interface, an extension method with the event's name prefixed by `When` is generated:

    public static Label WhenClicked(this Label self, EventHandler<EventArgs> handler)
    {
        self.Clicked += handler;
        return self;
    }
 
If the type of the event's delegate has an initial first parameter in the form of `Object sender`, the first parameter is promoted to be of the actual type of the object that flows through the method chain:

    public static Label WhenClicked(this Label self, Action<Label, EventArgs> handler)
    {
        self.Clicked += (arg0, arg1) => handler((Label)arg0, arg1);
        return self;
    }

Removing handlers from events that are added with AutoFluent extension methods is not supported. If required, the event has to be added by using the original API.

### Methods that return void

Methods that return void are prefixed with do `Do`, which is inspired by F# where the [keyword `do`](https://msdn.microsoft.com/en-us/library/dd393786.aspx) is used to execute code without defining a function or value.

For each public void method in a non-static class or interface, an extension method of the following form is generated:

    public static Label DoFocus(this Label self)
    {
        self.Focus();
        return self;
    }

Void methods in generic classes with generic type parameters and generic parameters are supported. Type constraints are added as needed.

### Methods that return a non-void value

Are not planned, because the importance of the return value can not be automatically classified, and generating an extension method for every method will probably make the generated class library too large.

## Design Notes

The initial design of AutoFluent added a type constraint to all extension methods that extend members of non-sealed classes to avoid the redeclaration of extension methods for each derived type. But type constraints are not taken into account in the overload resolution, which had the effect, that extension methods with the same name in a disjoint class hierarchy were ambiguous.

## Further ideas

### Propagate methods to generate mixins

Specifically in user interface APIs, properties that are named `Content` or `Children` are used to extend the control hierarchy. While it does not make sense to propagate `Content`'s methods to the declaring class, it could be convenient to add fluent extension methods like `AddTo` a container that supports a `Children` property with a type of `IList<Control>` for example.

## License & Copyright

License: BSD

Copyright © 2015 Armin Sander
