using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using AutoApiRouting = Omni.AutoApi.Routing;

namespace Omni.AutoApi.Analyzers
{
    /// <summary>
    /// Antecipa para a digitação erros de uso que hoje só aparecem no <b>startup</b> da aplicação
    /// (colisão de rota, múltiplos parâmetros de corpo) ou em <b>runtime</b> — mesma filosofia do
    /// <c>AUTOAPI001</c>, que já cobre o lado cliente.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AutoApiUsageAnalyzer : DiagnosticAnalyzer
    {
        private const string Category = "Omni.AutoApi";
        private const string RemoteServiceMetadataName = "Omni.AutoApi.IRemoteService";
        private const string GeneratedClientMetadataName = "Omni.AutoApi.AutoApiGeneratedClientAttribute";
        private const string RemoteStreamContentMetadataName = "Omni.AutoApi.RemoteStreamContent";

        internal static readonly DiagnosticDescriptor RouteCollision = new(
            id: "AUTOAPI002",
            title: "Sobrecarga gera rota duplicada",
            messageFormat: "Os métodos '{0}' geram a mesma rota '{1} {2}'; a aplicação falhará no startup. Renomeie um deles ou use [HttpGet(\"rota-unica\")]/[Route].",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "A rota é derivada do nome do método, então sobrecargas (e pares como Foo/FooAsync) colidem. A convenção detecta isso e lança InvalidOperationException ao iniciar a aplicação.");

        internal static readonly DiagnosticDescriptor MultipleBodyParameters = new(
            id: "AUTOAPI003",
            title: "Mais de um parâmetro de corpo",
            messageFormat: "'{0}' tem {1} parâmetros complexos ({2}) em um verbo com corpo; o MVC aceita apenas um [FromBody] e a aplicação falhará no startup",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Parâmetros complexos em POST/PUT/PATCH viram [FromBody]. Agrupe-os em um único DTO ou marque os demais com [FromQuery]/[FromRoute].");

        internal static readonly DiagnosticDescriptor NotTaskLike = new(
            id: "AUTOAPI004",
            title: "Método não pode ser consumido pelos clientes tipados",
            messageFormat: "'{0}' retorna '{1}'; o endpoint funciona, mas o proxy dinâmico e o cliente gerado exigem Task, Task<T>, ValueTask<T> ou IAsyncEnumerable<T>",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Os clientes tipados do Omni.AutoApi só implementam métodos assíncronos. Métodos síncronos continuam expostos por HTTP, mas precisam ser chamados manualmente.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(RouteCollision, MultipleBodyParameters, NotTaskLike);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationStart =>
            {
                var remoteService = compilationStart.Compilation.GetTypeByMetadataName(RemoteServiceMetadataName);
                if (remoteService is null)
                {
                    return;   // projeto não usa o Omni.AutoApi
                }

                var generatedClient = compilationStart.Compilation.GetTypeByMetadataName(GeneratedClientMetadataName);
                var remoteStream = compilationStart.Compilation.GetTypeByMetadataName(RemoteStreamContentMetadataName);

                compilationStart.RegisterSymbolAction(
                    symbolContext => AnalyzeType(symbolContext, remoteService, generatedClient, remoteStream),
                    SymbolKind.NamedType);
            });
        }

        private static void AnalyzeType(
            SymbolAnalysisContext context,
            INamedTypeSymbol remoteService,
            INamedTypeSymbol? generatedClient,
            INamedTypeSymbol? remoteStream)
        {
            var type = (INamedTypeSymbol)context.Symbol;

            if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic
                || type.DeclaredAccessibility != Accessibility.Public
                || !type.AllInterfaces.Contains(remoteService, SymbolEqualityComparer.Default))
            {
                return;
            }

            // Clientes gerados implementam a mesma interface, mas são o lado cliente.
            if (generatedClient is not null
                && type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, generatedClient)))
            {
                return;
            }

            var actions = type.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary
                            && m.DeclaredAccessibility == Accessibility.Public
                            && !m.IsStatic
                            && !m.IsImplicitlyDeclared)
                .ToList();

            ReportRouteCollisions(context, actions);

            foreach (var action in actions)
            {
                ReportMultipleBodyParameters(context, action, remoteStream);
                ReportNonTaskLikeReturn(context, action);
            }
        }

        /// <summary>
        /// AUTOAPI002 — a rota vem do NOME do método, então sobrecargas colidem. Também pega
        /// pares como <c>GetTodo</c>/<c>GetTodoAsync</c>, que derivam a mesma rota.
        /// </summary>
        private static void ReportRouteCollisions(SymbolAnalysisContext context, List<IMethodSymbol> actions)
        {
            var grupos = actions
                .Where(m => !TemRotaExplicita(m))
                .GroupBy(m => AutoApiRouting.ApiRouteBuilder.GetHttpMethod(m.Name)
                              + " /" + AutoApiRouting.ApiRouteBuilder.GetActionName(m.Name))
                .Where(g => g.Count() > 1);

            foreach (var grupo in grupos)
            {
                var nomes = string.Join("', '", grupo.Select(m => m.Name));
                var verbo = AutoApiRouting.ApiRouteBuilder.GetHttpMethod(grupo.First().Name);
                var rota = AutoApiRouting.ApiRouteBuilder.GetActionName(grupo.First().Name);

                foreach (var metodo in grupo)
                {
                    foreach (var location in metodo.Locations.Where(l => l.IsInSource))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(RouteCollision, location, nomes, verbo, rota));
                    }
                }
            }
        }

        /// <summary>AUTOAPI003 — o MVC só aceita um parâmetro vindo do corpo.</summary>
        private static void ReportMultipleBodyParameters(
            SymbolAnalysisContext context, IMethodSymbol action, INamedTypeSymbol? remoteStream)
        {
            if (!PermiteCorpo(action.Name))
            {
                return;
            }

            var complexos = action.Parameters
                .Where(p => !TemBindingExplicito(p)
                            && !EhCancellationToken(p.Type)
                            && !EhTipoSimples(p.Type)
                            && !EhIgnoradoNoCorpo(p.Type, remoteStream)
                            && p.Name != "id")   // 'id' nunca vai para o corpo (CanUseFormBodyBinding)
                .ToList();

            if (complexos.Count <= 1)
            {
                return;
            }

            var nomes = string.Join(", ", complexos.Select(p => p.Name));
            foreach (var location in action.Locations.Where(l => l.IsInSource))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MultipleBodyParameters, location, action.Name, complexos.Count, nomes));
            }
        }

        /// <summary>AUTOAPI004 — informativo: clientes tipados só implementam métodos assíncronos.</summary>
        private static void ReportNonTaskLikeReturn(SymbolAnalysisContext context, IMethodSymbol action)
        {
            if (EhAssincrono(action.ReturnType))
            {
                return;
            }

            foreach (var location in action.Locations.Where(l => l.IsInSource))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NotTaskLike, location, action.Name, action.ReturnType.ToDisplayString()));
            }
        }

        // ----------------- helpers -----------------

        /// <summary>
        /// Só um TEMPLATE de rota explícito evita a colisão: <c>[HttpGet]</c> sem template ainda
        /// cai na rota convencional (ver NormalizeSelectorRoutes).
        /// </summary>
        private static bool TemRotaExplicita(IMethodSymbol method)
        {
            foreach (var attribute in method.GetAttributes())
            {
                var nome = attribute.AttributeClass?.Name;
                if (nome is null)
                {
                    continue;
                }

                var ehRoteamento = nome == "RouteAttribute"
                    || (nome.StartsWith("Http", System.StringComparison.Ordinal) && nome.EndsWith("Attribute", System.StringComparison.Ordinal));

                if (ehRoteamento
                    && attribute.ConstructorArguments.Any(a => a.Value is string s && !string.IsNullOrWhiteSpace(s)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TemBindingExplicito(IParameterSymbol parameter)
        {
            return parameter.GetAttributes().Any(a =>
                a.AttributeClass?.Name is { } n
                && n.StartsWith("From", System.StringComparison.Ordinal)
                && n.EndsWith("Attribute", System.StringComparison.Ordinal));
        }

        private static bool PermiteCorpo(string methodName)
        {
            var verbo = AutoApiRouting.ApiRouteBuilder.GetHttpMethod(methodName);
            return verbo != "GET" && verbo != "DELETE" && verbo != "HEAD" && verbo != "TRACE";
        }

        private static bool EhCancellationToken(ITypeSymbol type)
            => type.Name == "CancellationToken"
               && type.ContainingNamespace?.ToDisplayString() == "System.Threading";

        private static bool EhIgnoradoNoCorpo(ITypeSymbol type, INamedTypeSymbol? remoteStream)
        {
            if (type.Name == "IFormFile")
            {
                return true;
            }

            return remoteStream is not null && SymbolEqualityComparer.Default.Equals(type, remoteStream);
        }

        private static bool EhAssincrono(ITypeSymbol returnType)
        {
            if (returnType is not INamedTypeSymbol named)
            {
                return false;
            }

            var ns = named.ContainingNamespace?.ToDisplayString();

            if (ns == "System.Threading.Tasks" && (named.Name == "Task" || named.Name == "ValueTask"))
            {
                return true;
            }

            return ns == "System.Collections.Generic" && named.Name == "IAsyncEnumerable";
        }

        /// <summary>Espelha TypeHelper.IsPrimitiveExtended para símbolos do Roslyn.</summary>
        private static bool EhTipoSimples(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named
                && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                && named.TypeArguments.Length == 1)
            {
                type = named.TypeArguments[0];
            }

            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Char:
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                case SpecialType.System_String:
                    return true;
            }

            if (type.TypeKind == TypeKind.Enum)
            {
                return true;
            }

            var fullName = (type.ContainingNamespace?.ToDisplayString() ?? string.Empty) + "." + type.Name;
            switch (fullName)
            {
                case "System.Guid":
                case "System.DateTime":
                case "System.DateTimeOffset":
                case "System.DateOnly":
                case "System.TimeOnly":
                case "System.TimeSpan":
                    return true;
            }

            return false;
        }
    }
}
