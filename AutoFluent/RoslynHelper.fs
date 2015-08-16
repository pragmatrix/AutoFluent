namespace AutoFluent

open System
open System.Reflection

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.MSBuild
open Microsoft.CodeAnalysis.CSharp
open Microsoft.CodeAnalysis.Editing

module RoslynHelper =

    [<AutoOpen>]
    module Helper = 

        let workspace = MSBuildWorkspace.Create()
        let sg = SyntaxGenerator.GetGenerator(workspace, "C#")
        
        let typeNameWithoutFuzz(gt: Type) : string = 
            let name = gt.GetGenericTypeDefinition().Name
            let i = name.IndexOf('`');
            if (i = -1) then name else name.Remove(i)

        let rec syntaxOfTypeName (t: Type) : SyntaxNode = 

            if (not t.IsGenericType) then
                sg.IdentifierName(t.FullName)
            else

            let name = typeNameWithoutFuzz(t)

            let genericTypes = 
                t.GetGenericArguments()
                |> Array.map syntaxOfTypeName
            
            let ns = sg.IdentifierName(t.Namespace)
            let tn = sg.GenericName(name, genericTypes)
            sg.QualifiedName(ns, tn)

    let typeName (t: Type) = 
        if t.IsGenericType then (syntaxOfTypeName(t).ToString()) else t.FullName