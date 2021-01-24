using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.Logging.SourceGenerator.Generator
{
    internal static class RoslynExtensions
    {
        public static bool IsException(this ITypeSymbol type, Compilation compilation)
        {
            return EnumerateBaseTypesAndSelf(type).Any(t => t.IsClrType(compilation, typeof(Exception)));
        }

        public static bool IsClrType(this ISymbol type, Compilation compilation, Type clrType)
            => type is ITypeSymbol ts &&
               ts.OriginalDefinition.Equals(compilation.GetTypeByMetadataName(clrType.FullName), SymbolEqualityComparer.Default);


        public static IEnumerable<ITypeSymbol> EnumerateBaseTypesAndSelf(ITypeSymbol type)
        {
            var t = type;
            while (t != null)
            {
                yield return t;
                t = t.BaseType;
            }
        }

        public static bool IsPartial(this IMethodSymbol methodSymbol) =>
            methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is MethodDeclarationSyntax methodDeclarationSyntax &&
            methodDeclarationSyntax.Modifiers.Any(s => s.IsKind(SyntaxKind.PartialKeyword));
    }
}
