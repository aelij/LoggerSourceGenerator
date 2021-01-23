using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Microsoft.Extensions.Logging.SourceGenerator.Generator
{
    [Generator]
    public class LoggerGenerator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor s_invalidFormatArgumentDiagnostic = new(
            id: "MEL001",
            title: "Invalid format argument",
            messageFormat: "The specified format argument '{0}' does not appear in the method parameter list or is an Exception type",
            category: "Correctness",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxReceiver receiver)
            {
                return;
            }

            var compilation = context.Compilation;
            foreach (var classDeclaration in receiver.Candidates)
            {
                var model = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                if (model.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol type ||
                    type.GetAttributes().FirstOrDefault(static a => a.AttributeClass?.Name == "LoggerAttribute") is not AttributeData attribute)
                {
                    continue;
                }

                context.AddSource(type.ToDisplayString(), GenerateType(context, type, attribute));
            }
        }

        private SourceText GenerateType(GeneratorExecutionContext context, INamedTypeSymbol type, AttributeData? attribute)
        {
            var builder = new StringBuilder();
            var isInterface = type.TypeKind == TypeKind.Interface;
            var typeName = type.Name;
            var loggerName = typeName;

            if (isInterface)
            {
                if (typeName.StartsWith("I"))
                {
                    loggerName = typeName = typeName.Substring(1);
                }

                typeName += "Implementation";
            }

            var ns = type.ContainingNamespace.ToDisplayString();

            if (attribute?.NamedArguments.FirstOrDefault(p => p.Key == "CategoryName").Value.Value is not string category)
            {
                category = $"{ns}.{loggerName}";
            }

            builder.Append(@$"
namespace {ns}
{{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.SourceGenerator;
    
    {(isInterface ? "internal" : "partial")} class {typeName}");

            if (isInterface)
            {
                builder.Append($" : {type.ToDisplayString()}");
            }

            builder.Append($@"
    {{
        private readonly ILogger _logger;

        public {typeName}(ILoggerFactory loggerFactory)
        {{
            _logger = loggerFactory.CreateLogger(""{category}"");
        }}
");
            foreach (var member in type.GetMembers())
            {
                if (member is not IMethodSymbol method ||
                    method.DeclaredAccessibility != Accessibility.Public ||
                    method.MethodKind != MethodKind.Ordinary ||
                    !method.ReturnsVoid)
                {
                    continue;
                }

                GenerateMethod(builder, context, method, isInterface);
            }

            builder.AppendLine(@$"
    }}
}}

namespace Microsoft.Extensions.DependencyInjection
{{
    public static class {loggerName}Extensions
    {{
        public static IServiceCollection Add{loggerName}(this IServiceCollection services) =>");
            if (isInterface)
            {
                builder.Append(@$"            services.AddSingleton<{ns}.{type.Name}, {ns}.{typeName}>();");
            }
            else
            {
                builder.Append(@$"            services.AddSingleton<{ns}.{type.Name}>();");
            }

            builder.Append(@$"
    }}
}}
");
            //System.Diagnostics.Debugger.Launch();
            return SourceText.From(builder.ToString(), Encoding.UTF8);
        }

        private static void GenerateMethod(StringBuilder builder, GeneratorExecutionContext context, IMethodSymbol method, bool isInterface)
        {
            var exceptionParameters = method.Parameters.Where(p => p.Type.IsException(context.Compilation)).ToImmutableArray();
            var formattedParameters = method.Parameters.Except(exceptionParameters).ToImmutableArray();

            var attribute = method.GetAttributes().FirstOrDefault(static a => a.AttributeClass?.Name == "LoggerEventAttribute");
            var logLevel = (LogLevel?)(attribute?.NamedArguments.FirstOrDefault(p => p.Key == "Level").Value.Value as int?) ??
                (exceptionParameters.Length > 0 ? LogLevel.Error : LogLevel.Information);
            var eventId = attribute?.NamedArguments.FirstOrDefault(p => p.Key == "EventId").Value.Value as int? ?? 0;

            var argArrayField = $"s_{method.Name}ArgumentNames";

            if (formattedParameters.Length > 0)
            {
                builder.AppendLine();
                builder.Append(@$"        private static readonly string[] {argArrayField} = new[] {{ ");
                AppendItems(builder, formattedParameters, p => @$"""{p.Name}""");
                builder.AppendLine(" };");
            }

            builder.AppendLine();

            builder.Append(@$"        public {(isInterface ? "" : "partial ")}void {method.Name}(");
            AppendItems(builder, method.Parameters, p => $"{p.ToDisplayString()} {p.Name}");

            builder.Append(@$")
        {{
            if (!_logger.IsEnabled(LogLevel.{logLevel})) return;
            _logger.Log(LogLevel.{logLevel}, new EventId({eventId}, ""{method.Name}""), ");
            if (formattedParameters.Length == 0)
            {
                builder.Append("LogValues.Empty");
            }
            else if (formattedParameters.Length == 1)
            {
                builder.Append("LogValues.AsLogValues(System.ValueTuple.Create(");
            }
            else
            {
                builder.Append("LogValues.AsLogValues((");
            }

            AppendItems(builder, formattedParameters, p => p.Name);
            if (formattedParameters.Length > 0)
            {
                builder.Append(@$"), {argArrayField})");
            }

            builder.Append(", ");
            AppendException();

            builder.Append(", ");

            if (attribute?.NamedArguments.FirstOrDefault(p => p.Key == "Format").Value.Value is string format)
            {
                AppendFormat();
            }
            else
            {
                AppendAutoFormat();
            }

            builder.AppendLine(@"
        }");

            void AppendException()
            {
                if (exceptionParameters.Length == 0)
                {
                    builder.Append("null");
                }
                else if (exceptionParameters.Length == 1)
                {
                    builder.Append(exceptionParameters[0].Name);
                }
                else
                {
                    builder.Append("new System.AggregateException(");
                    AppendItems(builder, exceptionParameters, p => p.Name);
                    builder.Append(")");
                }
            }

            // auto-formats log message as {method name}: {arg1 name}={arg1 value}, ...
            void AppendAutoFormat()
            {
                if (formattedParameters.Length == 0)
                {
                    builder.Append(@$"static (_, _) => ""{method.Name}"");");
                    return;
                }

                builder.Append(@"static (state, _) => $""");
                builder.Append(method.Name);
                builder.Append(": ");

                if (formattedParameters.Length == 1)
                {
                    builder.Append($"{formattedParameters[0].Name}={{FormatHelpers.FormatArgument(state.Values.Item1)}}");
                }
                else
                {
                    AppendItems(builder, formattedParameters, p => $"{p.Name}={{FormatHelpers.FormatArgument(state.Values.{p.Name})}}", separator: "; ");
                }

                builder.Append(@""");");
            }

            void AppendFormat()
            {
                var stringFormat = LogValuesFormatter.MakeFormat(format, formattedParameters.Select(p => p.Name).ToImmutableArray());

                if (!stringFormat.invalidArgs.IsEmpty)
                {
                    Location? location = null;
                    if (attribute?.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax attributeSyntax &&
                        attributeSyntax.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "Format") is AttributeArgumentSyntax formatArgument)
                    {
                        location = formatArgument.Expression.GetLocation();
                    }

                    foreach (var invalidArg in stringFormat.invalidArgs)
                    {
                        var argLocation = location == null || location.SourceTree == null ? null :
                            Location.Create(location.SourceTree, new TextSpan(location.SourceSpan.Start + invalidArg.start + 1, invalidArg.name.Length));
                        context.ReportDiagnostic(Diagnostic.Create(s_invalidFormatArgumentDiagnostic, argLocation, invalidArg.name));
                    }
                }

                var escapedFormat = stringFormat.format.Replace(@"""", @"""""");
                if (stringFormat.args.Length == 0)
                {
                    builder.Append(@$"static (state, _) => ""{escapedFormat}""");
                }
                else
                {
                    builder.Append(@$"static (state, _) => string.Format(@""{escapedFormat}"", ");
                    if (formattedParameters.Length == 1)
                    {
                        if (stringFormat.args.Length == 1)
                        {
                            builder.Append("FormatHelpers.FormatArgument(state.Values.Item1)");
                        }
                    }
                    else
                    {
                        AppendItems(builder, stringFormat.args, p => $"FormatHelpers.FormatArgument(state.Values.{p})");
                    }

                    builder.Append(")");
                }

                builder.Append(");");
            }
        }

        private static void AppendItems<T>(StringBuilder builder, IEnumerable<T> items, Func<T, string> formatter, string separator = ", ")
        {
            var first = true;
            foreach (var item in items)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    builder.Append(separator);
                }

                builder.Append(formatter(item));
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<SyntaxNode> Candidates { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                switch (syntaxNode)
                {
                    case ClassDeclarationSyntax classDeclaration when classDeclaration.AttributeLists.Count > 0:
                        Candidates.Add(classDeclaration);
                        break;
                    case InterfaceDeclarationSyntax interfaceDeclaration when interfaceDeclaration.AttributeLists.Count > 0:
                        Candidates.Add(interfaceDeclaration);
                        break;
                }
            }
        }
    }
}
