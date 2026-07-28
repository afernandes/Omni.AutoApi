using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Omni.AutoApi.Routing;

namespace Omni.AutoApi.Client.SourceGenerator
{
    /// <summary>
    /// Para cada interface marcada com <c>[AutoApiClient]</c>, gera uma implementação
    /// concreta de cliente HTTP (ex.: <c>ITodoAppService</c> → <c>TodoAppServiceClient</c>),
    /// reaproveitando a MESMA lógica de rota/verbo do servidor (<see cref="ApiRouteBuilder"/>,
    /// linkado neste projeto). É a alternativa estática e AOT-friendly ao proxy dinâmico.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public class AutoApiClientGenerator : IIncrementalGenerator
    {
        private const string AttributeMetadataName = "Omni.AutoApi.AutoApiClientAttribute";
        private const string RemoteStreamContentFullName = "Omni.AutoApi.RemoteStreamContent";

        private static readonly DiagnosticDescriptor UnsupportedMethod = new(
            id: "AUTOAPI001",
            title: "Método não suportado pelo cliente gerado",
            messageFormat: "O método '{0}' não pôde ser gerado ({1}); foi emitida uma implementação que lança NotSupportedException",
            category: "Omni.AutoApi.Client",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly SymbolDisplayFormat FullyQualified = SymbolDisplayFormat.FullyQualifiedFormat
            .WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        private enum ReturnKind
        {
            Unsupported,
            Task,
            TaskOfResult,
            AsyncStream
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(ctx =>
                ctx.AddSource("AutoApiGeneratedClientHelpers.g.cs", SourceText.From(HelpersSource, Encoding.UTF8)));

            // Opções de rota configuráveis por MSBuild (CompilerVisibleProperty).
            // Tupla para garantir igualdade estrutural (caching incremental correto).
            var routeConfig = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
            {
                var global = provider.GlobalOptions;
                var prefix = global.TryGetValue("build_property.AutoApiRoutePrefix", out var p) && !string.IsNullOrWhiteSpace(p)
                    ? p.Trim()
                    : "api/app-service";
                var kebab = !(global.TryGetValue("build_property.AutoApiUseKebabCase", out var k)
                    && bool.TryParse(k, out var parsed) && !parsed);
                // String crua na tupla => igualdade estrutural (caching incremental correto).
                var postfixes = global.TryGetValue("build_property.AutoApiControllerPostfixes", out var pf) && !string.IsNullOrWhiteSpace(pf)
                    ? pf.Trim()
                    : string.Empty;
                return (prefix, kebab, postfixes);
            });

            // Só emite as extensões de DI quando Microsoft.Extensions.DependencyInjection está
            // disponível na compilação. Com IHttpClientFactory presente (Microsoft.Extensions.Http),
            // o registro usa AddHttpClient (pooling/resiliência); senão, um fallback leve.
            var diCapabilities = context.CompilationProvider.Select(static (compilation, _) =>
                (di: compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection") != null,
                 httpFactory: compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.HttpClientFactoryServiceCollectionExtensions") != null));

            var interfaces = context.SyntaxProvider.ForAttributeWithMetadataName(
                AttributeMetadataName,
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol);

            context.RegisterSourceOutput(interfaces.Combine(routeConfig).Combine(diCapabilities),
                static (spc, t) => Execute(spc, t.Left.Left, t.Left.Right, t.Right));

            // Agregado: AddAllAutoApiClients registra todos os clientes do assembly de uma vez.
            var clients = interfaces.Select(static (s, _) =>
                (clientName: StripInterfacePrefix(s.Name) + "Client",
                 ifaceFq: s.ToDisplayString(FullyQualified))).Collect();
            context.RegisterSourceOutput(clients.Combine(diCapabilities),
                static (spc, t) => EmitAggregate(spc, t.Left, t.Right));
        }

        private static void Execute(
            SourceProductionContext context,
            INamedTypeSymbol iface,
            (string prefix, bool kebab, string postfixes) config,
            (bool di, bool httpFactory) capabilities)
        {
            var routeOptions = new RouteOptions { Prefix = config.prefix, UseKebabCase = config.kebab };
            if (!string.IsNullOrWhiteSpace(config.postfixes))
            {
                routeOptions.ControllerPostfixes = config.postfixes
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToArray();
            }

            var ns = iface.ContainingNamespace.IsGlobalNamespace
                ? null
                : iface.ContainingNamespace.ToDisplayString();

            var ifaceFq = iface.ToDisplayString(FullyQualified);
            var clientName = StripInterfacePrefix(iface.Name) + "Client";
            var clientFq = (ns != null ? $"global::{ns}." : "global::") + clientName;
            var methods = CollectMethods(iface);

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            var indent = "";
            if (ns != null)
            {
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
                indent = "    ";
            }

            sb.AppendLine($"{indent}/// <summary>Cliente HTTP gerado para <see cref=\"{ifaceFq}\"/>.</summary>");
            sb.AppendLine($"{indent}[global::Omni.AutoApi.AutoApiGeneratedClientAttribute]");
            sb.AppendLine($"{indent}public partial class {clientName} : {ifaceFq}");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    private readonly global::System.Net.Http.HttpClient _httpClient;");
            sb.AppendLine($"{indent}    private readonly global::System.Text.Json.JsonSerializerOptions _jsonOptions;");
            sb.AppendLine();
            sb.AppendLine($"{indent}    public {clientName}(global::System.Net.Http.HttpClient httpClient, global::System.Text.Json.JsonSerializerOptions? jsonOptions = null)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        _httpClient = httpClient ?? throw new global::System.ArgumentNullException(nameof(httpClient));");
            sb.AppendLine($"{indent}        _jsonOptions = jsonOptions ?? new global::System.Text.Json.JsonSerializerOptions(global::System.Text.Json.JsonSerializerDefaults.Web);");
            sb.AppendLine($"{indent}        // Normaliza a barra final: sem ela, o Uri combinaria BaseAddress+rota descartando o último segmento.");
            sb.AppendLine($"{indent}        if (httpClient.BaseAddress is {{ }} __baseAddress && !__baseAddress.AbsoluteUri.EndsWith(\"/\", global::System.StringComparison.Ordinal))");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            httpClient.BaseAddress = new global::System.Uri(__baseAddress.AbsoluteUri + \"/\");");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");

            foreach (var method in methods)
            {
                sb.AppendLine();
                EmitMethod(context, sb, method, iface.Name, routeOptions, indent + "    ");
            }

            sb.AppendLine($"{indent}}}");
            if (ns != null)
            {
                sb.AppendLine("}");
            }

            // Extensão de DI por-cliente (ex.: services.AddTodoAppServiceClient(...)).
            if (capabilities.di)
            {
                EmitClientRegistration(sb, clientName, ifaceFq, clientFq, capabilities.httpFactory);
            }

            var hint = (ns != null ? ns + "." : string.Empty) + clientName + ".g.cs";
            context.AddSource(hint, SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        /// <summary>
        /// Emite uma extensão <c>services.Add{Cliente}(configure)</c>. Quando Microsoft.Extensions.Http
        /// está presente, usa <c>AddHttpClient</c> + typed client (pooling, resiliência, handlers via
        /// IHttpClientBuilder); senão, um fallback leve (AddScoped + HttpClient próprio) apenas com
        /// DI.Abstractions.
        /// </summary>
        private static void EmitClientRegistration(StringBuilder sb, string clientName, string ifaceFq, string clientFq, bool httpFactory)
        {
            var clientKey = ifaceFq.StartsWith("global::", StringComparison.Ordinal)
                ? ifaceFq.Substring("global::".Length)
                : ifaceFq;

            sb.AppendLine();
            sb.AppendLine("namespace Omni.AutoApi.Client.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public static partial class GeneratedClientRegistrations");
            sb.AppendLine("    {");

            if (httpFactory)
            {
                sb.AppendLine($"        /// <summary>Registra <see cref=\"{ifaceFq}\"/> via IHttpClientFactory (typed client).");
                sb.AppendLine("        /// Encadeie resiliência/handlers no IHttpClientBuilder retornado. Para JsonSerializerOptions");
                sb.AppendLine("        /// custom, registre-o como singleton no DI — ele é injetado no cliente gerado.</summary>");
                sb.AppendLine($"        public static global::Microsoft.Extensions.DependencyInjection.IHttpClientBuilder Add{clientName}(");
                sb.AppendLine("            this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services,");
                sb.AppendLine("            global::System.Action<global::System.IServiceProvider, global::System.Net.Http.HttpClient> configureClient)");
                sb.AppendLine("        {");
                sb.AppendLine($"            var builder = global::Microsoft.Extensions.DependencyInjection.HttpClientFactoryServiceCollectionExtensions.AddHttpClient(services, \"{clientKey}\", configureClient);");
                sb.AppendLine($"            return global::Microsoft.Extensions.DependencyInjection.HttpClientBuilderExtensions.AddTypedClient<{ifaceFq}>(");
                sb.AppendLine("                builder,");
                sb.AppendLine($"                static (httpClient, serviceProvider) => new {clientFq}(");
                sb.AppendLine("                    httpClient,");
                sb.AppendLine("                    (global::System.Text.Json.JsonSerializerOptions?)serviceProvider.GetService(typeof(global::System.Text.Json.JsonSerializerOptions))));");
                sb.AppendLine("        }");
            }
            else
            {
                sb.AppendLine($"        /// <summary>Registra <see cref=\"{ifaceFq}\"/> resolvido pelo cliente gerado (fallback sem");
                sb.AppendLine("        /// IHttpClientFactory). Em produção, prefira referenciar Microsoft.Extensions.Http no projeto");
                sb.AppendLine("        /// de contratos para que este registro use AddHttpClient (pooling de conexões).</summary>");
                sb.AppendLine($"        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection Add{clientName}(");
                sb.AppendLine("            this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services,");
                sb.AppendLine("            global::System.Action<global::System.IServiceProvider, global::System.Net.Http.HttpClient> configureClient,");
                sb.AppendLine("            global::System.Text.Json.JsonSerializerOptions? jsonOptions = null)");
                sb.AppendLine("        {");
                sb.AppendLine($"            return global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<{ifaceFq}>(");
                sb.AppendLine("                services,");
                sb.AppendLine("                serviceProvider =>");
                sb.AppendLine("                {");
                sb.AppendLine("                    var httpClient = new global::System.Net.Http.HttpClient();");
                sb.AppendLine("                    configureClient(serviceProvider, httpClient);");
                sb.AppendLine($"                    return new {clientFq}(httpClient, jsonOptions);");
                sb.AppendLine("                });");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
        }

        /// <summary>
        /// Emite <c>services.AddAllAutoApiClients(...)</c>: registra cada Add{Cliente} do assembly.
        /// Overloads: configuração única (backend único) e por-serviço (multi-backend, o callback
        /// recebe o Type da interface remota).
        /// </summary>
        private static void EmitAggregate(
            SourceProductionContext context,
            ImmutableArray<(string clientName, string ifaceFq)> clients,
            (bool di, bool httpFactory) capabilities)
        {
            if (!capabilities.di || clients.IsEmpty)
            {
                return;
            }

            var ordered = clients.Distinct().OrderBy(c => c.clientName, StringComparer.Ordinal).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("namespace Omni.AutoApi.Client.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public static partial class GeneratedClientRegistrations");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>Registra TODOS os clientes gerados deste assembly com a mesma configuração de HttpClient.</summary>");
            sb.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddAllAutoApiClients(");
            sb.AppendLine("            this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services,");
            sb.AppendLine("            global::System.Action<global::System.IServiceProvider, global::System.Net.Http.HttpClient> configureClient)");
            sb.AppendLine("        {");
            foreach (var (name, _) in ordered)
            {
                sb.AppendLine($"            Add{name}(services, configureClient);");
            }
            sb.AppendLine("            return services;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Variante com configuração POR SERVIÇO: o callback recebe o Type da interface");
            sb.AppendLine("        /// remota, permitindo BaseAddress/headers distintos por backend (multi-serviço).</summary>");
            sb.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddAllAutoApiClients(");
            sb.AppendLine("            this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services,");
            sb.AppendLine("            global::System.Action<global::System.IServiceProvider, global::System.Net.Http.HttpClient, global::System.Type> configureClient)");
            sb.AppendLine("        {");
            foreach (var (name, ifaceFq) in ordered)
            {
                sb.AppendLine($"            Add{name}(services, (serviceProvider, httpClient) => configureClient(serviceProvider, httpClient, typeof({ifaceFq})));");
            }
            sb.AppendLine("            return services;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource("AutoApiClientRegistrations.Aggregate.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static void EmitMethod(SourceProductionContext context, StringBuilder sb, IMethodSymbol method, string interfaceName, RouteOptions routeOptions, string indent)
        {
            var (returnKind, resultFq) = AnalyzeReturn(method.ReturnType);

            // Identifica formatos não suportados e emite um stub que implementa o membro
            // (mantém a classe compilável) + um diagnóstico claro, em vez de código quebrado.
            var bodyEligible = method.Parameters
                .Count(p => !IsCancellationToken(p.Type) && !IsSimpleType(p.Type));
            var streamParam = method.Parameters.FirstOrDefault(p => IsRemoteStreamContent(p.Type));

            string? unsupportedReason = null;
            if (method.IsGenericMethod)
            {
                unsupportedReason = "métodos genéricos não são suportados";
            }
            else if (returnKind == ReturnKind.Unsupported)
            {
                unsupportedReason = "o tipo de retorno deve ser Task, Task<T>/ValueTask<T> ou IAsyncEnumerable<T>";
            }
            else if (method.Parameters.Any(p => IsRawStreamLike(p.Type)))
            {
                unsupportedReason = "parâmetros Stream/IFormFile não são suportados no cliente; use Omni.AutoApi.RemoteStreamContent";
            }
            else if (streamParam != null && !IsBodyVerb(method))
            {
                unsupportedReason = "upload (RemoteStreamContent) requer um verbo com corpo (POST/PUT/PATCH)";
            }
            else if (IsBodyVerb(method) && bodyEligible > 1)
            {
                unsupportedReason = "métodos com mais de um parâmetro complexo (corpo) não são suportados";
            }

            if (unsupportedReason != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedMethod, method.Locations.FirstOrDefault() ?? Location.None, method.Name, unsupportedReason));
                EmitUnsupportedStub(sb, method, indent, unsupportedReason);
                return;
            }

            var verb = ApiRouteBuilder.GetHttpMethod(method.Name);
            var action = ApiRouteBuilder.GetActionName(method.Name, routeOptions);
            var route = ApiRouteBuilder.GetApiServiceRouteFromInterface(interfaceName, action, routeOptions);
            var allowsBody = IsBodyVerb(method);

            var cancellationToken = "default";
            string? bodyParameter = null;
            string? streamParameterVar = null;
            string? streamParameterName = null;
            var queryStatements = new List<string>();

            foreach (var parameter in method.Parameters)
            {
                var variable = Escape(parameter.Name);

                if (IsCancellationToken(parameter.Type))
                {
                    cancellationToken = variable;
                    continue;
                }

                if (IsSimpleType(parameter.Type))
                {
                    queryStatements.Add($"{indent}    global::Omni.AutoApi.Client.Generated.GeneratedClientHelpers.Add(__query, \"{parameter.Name}\", {variable});");
                }
                else if (IsRemoteStreamContent(parameter.Type))
                {
                    streamParameterVar = variable;
                    streamParameterName = parameter.Name;
                }
                else if (allowsBody)
                {
                    bodyParameter = variable;
                }
                else
                {
                    queryStatements.Add($"{indent}    global::Omni.AutoApi.Client.Generated.GeneratedClientHelpers.AddObject(__query, {variable});");
                }
            }

            var isStreamReturn = returnKind == ReturnKind.AsyncStream;
            sb.AppendLine($"{indent}public async {Signature(method, annotateEnumeratorCancellation: isStreamReturn)}");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    var __query = new global::System.Collections.Generic.List<string>();");
            foreach (var statement in queryStatements)
            {
                sb.AppendLine(statement);
            }
            sb.AppendLine($"{indent}    var __url = \"{route}\";");
            sb.AppendLine($"{indent}    if (__query.Count > 0) {{ __url += \"?\" + string.Join(\"&\", __query); }}");
            sb.AppendLine($"{indent}    using var __request = new global::System.Net.Http.HttpRequestMessage({HttpMethodExpression(verb)}, __url);");

            if (streamParameterVar != null)
            {
                sb.AppendLine($"{indent}    if ({streamParameterVar} is not null)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        var __multipart = new global::System.Net.Http.MultipartFormDataContent();");
                sb.AppendLine($"{indent}        var __streamContent = new global::System.Net.Http.StreamContent({streamParameterVar}.Stream);");
                sb.AppendLine($"{indent}        if ({streamParameterVar}.ContentType is not null)");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            __streamContent.Headers.ContentType = new global::System.Net.Http.Headers.MediaTypeHeaderValue({streamParameterVar}.ContentType);");
                sb.AppendLine($"{indent}        }}");
                sb.AppendLine($"{indent}        __multipart.Add(__streamContent, \"{streamParameterName}\", {streamParameterVar}.FileName ?? \"{streamParameterName}\");");
                sb.AppendLine($"{indent}        __request.Content = __multipart;");
                sb.AppendLine($"{indent}    }}");
            }
            else if (bodyParameter != null)
            {
                sb.AppendLine($"{indent}    __request.Content = global::System.Net.Http.Json.JsonContent.Create({bodyParameter}, (global::System.Net.Http.Headers.MediaTypeHeaderValue?)null, _jsonOptions);");
            }

            if (isStreamReturn)
            {
                sb.AppendLine($"{indent}    using var __response = await _httpClient.SendAsync(__request, global::System.Net.Http.HttpCompletionOption.ResponseHeadersRead, {cancellationToken}).ConfigureAwait(false);");
                sb.AppendLine($"{indent}    __response.EnsureSuccessStatusCode();");
                sb.AppendLine($"{indent}    using var __stream = await __response.Content.ReadAsStreamAsync({cancellationToken}).ConfigureAwait(false);");
                sb.AppendLine($"{indent}    await foreach (var __item in global::System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable<{resultFq}>(__stream, _jsonOptions, {cancellationToken}).ConfigureAwait(false))");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        yield return __item!;");
                sb.AppendLine($"{indent}    }}");
            }
            else
            {
                sb.AppendLine($"{indent}    using var __response = await _httpClient.SendAsync(__request, {cancellationToken}).ConfigureAwait(false);");
                sb.AppendLine($"{indent}    __response.EnsureSuccessStatusCode();");
                if (returnKind == ReturnKind.TaskOfResult)
                {
                    sb.AppendLine($"{indent}    if (__response.StatusCode == global::System.Net.HttpStatusCode.NoContent || __response.Content.Headers.ContentLength == 0) {{ return default!; }}");
                    if (resultFq == "string" || resultFq == "string?")
                    {
                        // MVC serializa Task<string> como text/plain (StringOutputFormatter).
                        sb.AppendLine($"{indent}    if (!string.Equals(__response.Content.Headers.ContentType?.MediaType, \"application/json\", global::System.StringComparison.OrdinalIgnoreCase))");
                        sb.AppendLine($"{indent}    {{");
                        sb.AppendLine($"{indent}        return (await __response.Content.ReadAsStringAsync({cancellationToken}).ConfigureAwait(false))!;");
                        sb.AppendLine($"{indent}    }}");
                    }
                    sb.AppendLine($"{indent}    return (await global::System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<{resultFq}>(__response.Content, _jsonOptions, {cancellationToken}).ConfigureAwait(false))!;");
                }
            }

            sb.AppendLine($"{indent}}}");
        }

        private static void EmitUnsupportedStub(StringBuilder sb, IMethodSymbol method, string indent, string reason)
        {
            sb.AppendLine($"{indent}public {Signature(method)}");
            sb.AppendLine($"{indent}    => throw new global::System.NotSupportedException(\"{method.Name}: {reason}.\");");
        }

        /// <summary>Assinatura "{retorno} {nome}{&lt;genéricos&gt;}({params})" com nomes escapados.</summary>
        private static string Signature(IMethodSymbol method, bool annotateEnumeratorCancellation = false)
        {
            var returnFq = method.ReturnType.ToDisplayString(FullyQualified);
            var generics = method.IsGenericMethod
                ? "<" + string.Join(", ", method.TypeParameters.Select(t => t.Name)) + ">"
                : string.Empty;
            var parameters = string.Join(", ",
                method.Parameters.Select(p =>
                {
                    var annotation = annotateEnumeratorCancellation && IsCancellationToken(p.Type)
                        ? "[global::System.Runtime.CompilerServices.EnumeratorCancellation] "
                        : string.Empty;
                    return $"{annotation}{p.Type.ToDisplayString(FullyQualified)} {Escape(p.Name)}{DefaultValueSuffix(p)}";
                }));

            return $"{returnFq} {method.Name}{generics}({parameters})";
        }

        /// <summary>Propaga valores default da interface (ex.: CancellationToken ct = default).</summary>
        private static string DefaultValueSuffix(IParameterSymbol parameter)
        {
            if (!parameter.HasExplicitDefaultValue)
            {
                return string.Empty;
            }

            var value = parameter.ExplicitDefaultValue;
            if (value is null)
            {
                return " = default";
            }

            if (parameter.Type.TypeKind == TypeKind.Enum)
            {
                return $" = ({parameter.Type.ToDisplayString(FullyQualified)}){Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)}";
            }

            return value switch
            {
                bool b => b ? " = true" : " = false",
                string s => " = \"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
                char c => " = '" + c + "'",
                _ => " = " + Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        private static IReadOnlyList<IMethodSymbol> CollectMethods(INamedTypeSymbol iface)
        {
            var all = iface.GetMembers().OfType<IMethodSymbol>()
                .Concat(iface.AllInterfaces.SelectMany(i => i.GetMembers().OfType<IMethodSymbol>()));

            var result = new List<IMethodSymbol>();
            var seen = new HashSet<string>();
            foreach (var method in all)
            {
                if (method.MethodKind != MethodKind.Ordinary)
                {
                    continue;
                }

                var key = method.Name + "`" + method.TypeParameters.Length + "("
                    + string.Join(",", method.Parameters.Select(p => p.Type.ToDisplayString(FullyQualified))) + ")";
                if (seen.Add(key))
                {
                    result.Add(method);
                }
            }

            return result;
        }

        private static (ReturnKind kind, string resultFq) AnalyzeReturn(ITypeSymbol returnType)
        {
            if (returnType is not INamedTypeSymbol named)
            {
                return (ReturnKind.Unsupported, string.Empty);
            }

            var containingNamespace = named.ContainingNamespace?.ToDisplayString();

            if (containingNamespace == "System.Threading.Tasks"
                && (named.Name == "Task" || named.Name == "ValueTask"))
            {
                if (named.IsGenericType && named.TypeArguments.Length == 1)
                {
                    return (ReturnKind.TaskOfResult, named.TypeArguments[0].ToDisplayString(FullyQualified));
                }

                return (ReturnKind.Task, string.Empty);
            }

            if (containingNamespace == "System.Collections.Generic"
                && named.Name == "IAsyncEnumerable"
                && named.TypeArguments.Length == 1)
            {
                return (ReturnKind.AsyncStream, named.TypeArguments[0].ToDisplayString(FullyQualified));
            }

            return (ReturnKind.Unsupported, string.Empty);
        }

        private static bool IsBodyVerb(IMethodSymbol method)
        {
            var verb = ApiRouteBuilder.GetHttpMethod(method.Name);
            return !(verb == "GET" || verb == "DELETE" || verb == "HEAD" || verb == "TRACE");
        }

        private static bool IsSimpleType(ITypeSymbol type)
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

        private static bool IsCancellationToken(ITypeSymbol type)
        {
            return type.Name == "CancellationToken"
                && type.ContainingNamespace?.ToDisplayString() == "System.Threading";
        }

        private static bool IsRemoteStreamContent(ITypeSymbol type)
        {
            return type.Name == "RemoteStreamContent"
                && (type.ContainingNamespace?.ToDisplayString() ?? string.Empty) + "." + type.Name == RemoteStreamContentFullName;
        }

        /// <summary>Stream/IFormFile crus não são suportados no contrato (use RemoteStreamContent).</summary>
        private static bool IsRawStreamLike(ITypeSymbol type)
        {
            if (type.Name == "IFormFile")
            {
                return true;
            }

            for (var current = type; current != null; current = (current as INamedTypeSymbol)?.BaseType)
            {
                if (current.Name == "Stream"
                    && current.ContainingNamespace?.ToDisplayString() == "System.IO")
                {
                    return true;
                }
            }

            return false;
        }

        private static string StripInterfacePrefix(string name)
        {
            if (name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
            {
                return name.Substring(1);
            }

            return name;
        }

        private static string Escape(string identifier)
        {
            return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
                ? "@" + identifier
                : identifier;
        }

        private static string HttpMethodExpression(string verb)
        {
            switch (verb)
            {
                case "GET": return "global::System.Net.Http.HttpMethod.Get";
                case "POST": return "global::System.Net.Http.HttpMethod.Post";
                case "PUT": return "global::System.Net.Http.HttpMethod.Put";
                case "DELETE": return "global::System.Net.Http.HttpMethod.Delete";
                case "PATCH": return "global::System.Net.Http.HttpMethod.Patch";
                case "HEAD": return "global::System.Net.Http.HttpMethod.Head";
                case "OPTIONS": return "global::System.Net.Http.HttpMethod.Options";
                default: return $"new global::System.Net.Http.HttpMethod(\"{verb}\")";
            }
        }

        private const string HelpersSource = @"// <auto-generated/>
#nullable enable
namespace Omni.AutoApi.Client.Generated
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;

    /// <summary>Helpers de runtime compartilhados pelos clientes HTTP gerados (autossuficiente).</summary>
    internal static class GeneratedClientHelpers
    {
        public static void Add(List<string> query, string name, object? value)
        {
            if (value is null) return;
            query.Add(Uri.EscapeDataString(name) + ""="" + Uri.EscapeDataString(Format(value)));
        }

        /// <summary>
        /// Espalha propriedades públicas simples (e coleções de simples) na query string.
        /// LIMITAÇÃO: achata apenas 1 nível — propriedades complexas aninhadas são omitidas.
        /// </summary>
        public static void AddObject(List<string> query,
#if NET5_0_OR_GREATER
            [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            object? value)
        {
            if (value is null) return;
            foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead) continue;
                var propertyValue = property.GetValue(value);
                if (propertyValue is null) continue;

                if (IsSimple(property.PropertyType))
                {
                    Add(query, property.Name, propertyValue);
                }
                else if (propertyValue is IEnumerable enumerable)
                {
                    foreach (var item in enumerable) Add(query, property.Name, item);
                }
            }
        }

        // Contrato de query string: datas/horas em ISO-8601 invariante (""O""), enums pelo NOME.
        public static string Format(object value) => value switch
        {
            bool b => b ? ""true"" : ""false"",
            DateTime dt => dt.ToString(""O"", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString(""O"", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString(""O"", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString(""O"", CultureInfo.InvariantCulture),
            Enum e => e.ToString(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

        private static bool IsSimple(Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            if (t.IsPrimitive || t.IsEnum) return true;
            return t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime)
                || t == typeof(DateTimeOffset) || t == typeof(DateOnly) || t == typeof(TimeOnly)
                || t == typeof(TimeSpan) || t == typeof(Guid);
        }
    }
}
";
    }
}
