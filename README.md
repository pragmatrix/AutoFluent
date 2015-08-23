# AutoFluent - A fluent API generator

[![Build status](https://ci.appveyor.com/api/projects/status/ornm2laos0d7bio1?svg=true)](https://ci.appveyor.com/project/pragmatrix/autofluent)

AutoFluent takes a .NET assembly and generates extension methods for properties and events, so that they can be used fluently in a method chain.

# Installation

## AutoFluent command line tool

TBD

## Packages

AutoFluent generated API packages will be released on NuGet. The naming convention of the generated packages is `[Name].AutoFluent`, where `[Name]` is the original API's name, assembly, or primary namespace. 

To ease the transition to a fluent programming style, AutoFluent packages will have one dependency to a very small portable class library that contains all the code in [Collections.Fluent](https://github.com/pragmatrix/Collections.Fluent) and some more methods that are useful in method chains.

# Generators

Right now, AutoFluent generates extension methods for properties and events. Methods without a return value are planned, too, but are not implemented yet.

## Properties

For each public property in a non-static class or interface, an extension method with the same name and the property type as its only parameter is generated. The method body assigns the property and returns the instance of the class.

A typical extension method for a simple property looks like this:

    public static Label Title(this Label self, string value)
    {
        self.Title = value;
        return self;
    }

If the property is declared in a non-sealed class, a generic method is generated and a type constraint is added, so that instances of derived classes do not lose their type in the method chain:

    public static SelfT Title<SelfT>(this SelfT self, string value)
        where SelfT : Label
    {
        self.Title = value;
        return self;
    }

The generator can handle generic properties in generic classes and will add type parameters and constraints as needed.

## Events

For each public event in a non-static class or interface, an extension method with the event's name prefixed by `When` is generated:

    public static Label WhenClicked(this Label self, EventHandler<EventArgs> handler)
    {
        self.Clicked += handler;
        return self;
    }
 
Like with properties, extension methods for events in non-sealed classes are generic and use a type constraint that allows derived types to propagate through the method chain:

    public static SelfT WhenClicked(this SelfT self, EventHandler<EventArgs> handler)
        where SelfT : Label
    {
        self.Clicked += handler;
        return self;
    }

If the type of the event's delegate has an initial first parameter in the form of `Object sender`, the first parameter is promoted to be of the actual type of the object that flows through the method chain:

    public static SelfT WhenClicked(this SelfT self, Action<SelfT, EventArgs> handler)
        where SelfT : Label
    {
        self.Clicked += (arg0, arg1) => handler((SelfT)arg0, arg1);
        return self;
    }

Removing handlers from events that are added with AutoFluent extension methods is not supported. If required, the event has to be added by using the original API.


## Methods that return void

This generator is not implemented yet. The current proposal is to prefix them with `Do` which is inspired by F# where the [keyword `do`](https://msdn.microsoft.com/en-us/library/dd393786.aspx) is used to execute code without defining a function or value.

## Methods that return a non-void value

Are not planned, because the importance of the return value can not be automatically classified, and generating an extension method for every method will probably make the generated class library too large.

# License & Copyright

License: BSD

Copyright © 2015 Armin Sander