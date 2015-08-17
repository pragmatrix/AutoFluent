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

        let typeNameOfNonGenericType(t: Type) : string = 
            assert(not t.IsGenericType)
            // even if it is not a generic type, it may be a generic parameter
            if (t.IsGenericParameter) then t.Name
            else t.FullName

        let typeNameWithoutFuzz(gt: Type) : string = 
            if not gt.IsGenericType then gt.Name
            else
            let name = gt.GetGenericTypeDefinition().Name
            let i = name.IndexOf('`');
            if (i = -1) then name else name.Remove(i)

        let rec syntaxOfTypeName (t: Type) : SyntaxNode = 

            if (not t.IsGenericType) then
                sg.IdentifierName(typeNameOfNonGenericType(t))
            else

            let name = typeNameWithoutFuzz(t)

            let arguments = 
                t.GetGenericArguments()
                |> Array.map syntaxOfTypeName
            
            let ns = sg.IdentifierName(t.Namespace)
            let tn = sg.GenericName(name, arguments)
            sg.QualifiedName(ns, tn)

    let typeName (t: Type) = 
        if t.IsGenericType then (syntaxOfTypeName(t).ToString()) else typeNameOfNonGenericType t

    let replaceTypeName (newName: string) (prototype: Type) = 
        if not prototype.IsGenericType then newName
        else
        let parameters = 
            prototype.GetTypeInfo().GenericTypeParameters
            |> Array.map syntaxOfTypeName

        sg.GenericName(newName, parameters).ToString()



