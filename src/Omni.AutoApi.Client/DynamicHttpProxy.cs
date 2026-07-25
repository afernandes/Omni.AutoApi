using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Omni.AutoApi.Routing;

namespace Omni.AutoApi.Client
{
    /// <summary>
    /// Proxy de cliente HTTP dinâmico, no espírito do <c>DynamicHttpProxyInterceptor</c> do ABP.
    /// Implementa em runtime uma interface remota (<typeparamref name="T"/>) traduzindo cada
    /// chamada de método em uma requisição HTTP, reaproveitando EXATAMENTE as mesmas regras de
    /// rota/verbo/binding do servidor (<see cref="ApiRouteBuilder"/> + <see cref="TypeHelper"/>).
    /// A <b>rota/verbo</b> são derivados do mesmo algoritmo dos dois lados, então não saem de
    /// sincronia. Já a <b>assinatura</b> (parâmetros/tipos) NÃO é validada contra o servidor:
    /// a mesma interface deve ser compartilhada por ambos (idealmente num assembly de contratos).
    /// </summary>
    /// <typeparam name="T">A interface remota (deve ser uma interface; exigência do DispatchProxy).</typeparam>
    public class DynamicHttpProxy<T> : DispatchProxy where T : class
    {
        private static readonly MethodInfo InvokeResultAsyncMethod =
            typeof(DynamicHttpProxy<T>).GetMethod(nameof(InvokeResultAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly MethodInfo InvokeStreamAsyncMethod =
            typeof(DynamicHttpProxy<T>).GetMethod(nameof(InvokeStreamAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly string[] NoBodyHttpMethods = { "GET", "DELETE", "TRACE", "HEAD" };

        private HttpClient _httpClient = null!;
        private JsonSerializerOptions _jsonOptions = null!;
        private RouteOptions _routeOptions = RouteOptions.Default;

        public static T Create(HttpClient httpClient, JsonSerializerOptions? jsonOptions = null, RouteOptions? routeOptions = null)
        {
            ArgumentNullException.ThrowIfNull(httpClient);

            // Normaliza a barra final: sem ela, Uri combina "https://host/app" + "api/x"
            // descartando o último segmento ("https://host/api/x") silenciosamente.
            if (httpClient.BaseAddress is { } baseAddress
                && !baseAddress.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
            {
                httpClient.BaseAddress = new Uri(baseAddress.AbsoluteUri + "/");
            }

            var proxy = Create<T, DynamicHttpProxy<T>>();
            var instance = (DynamicHttpProxy<T>)(object)proxy!;
            instance._httpClient = httpClient;
            instance._jsonOptions = jsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
            instance._routeOptions = routeOptions ?? RouteOptions.Default;
            return proxy!;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            args ??= Array.Empty<object?>();

            var returnType = targetMethod.ReturnType;

            if (returnType == typeof(Task))
            {
                return InvokeAsync(targetMethod, args);
            }

            if (returnType.IsGenericType)
            {
                var definition = returnType.GetGenericTypeDefinition();

                if (definition == typeof(Task<>))
                {
                    return InvokeResultAsyncMethod
                        .MakeGenericMethod(returnType.GetGenericArguments()[0])
                        .Invoke(this, new object[] { targetMethod, args });
                }

                if (definition == typeof(IAsyncEnumerable<>))
                {
                    return InvokeStreamAsyncMethod
                        .MakeGenericMethod(returnType.GetGenericArguments()[0])
                        .Invoke(this, new object[] { targetMethod, args });
                }
            }

            throw new NotSupportedException(
                $"O método '{targetMethod.Name}' deve retornar Task, Task<T> ou IAsyncEnumerable<T> para ser usado pelo proxy dinâmico.");
        }

        private async Task InvokeAsync(MethodInfo method, object?[] args)
        {
            using var request = BuildRequest(method, args, out var cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private async Task<TResult> InvokeResultAsync<TResult>(MethodInfo method, object?[] args)
        {
            using var request = BuildRequest(method, args, out var cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            if (response.StatusCode == HttpStatusCode.NoContent
                || response.Content.Headers.ContentLength == 0)
            {
                return default!;
            }

            // MVC serializa Task<string> como text/plain (StringOutputFormatter);
            // nesse caso o corpo é a string crua, não JSON.
            if (typeof(TResult) == typeof(string)
                && !string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                return (TResult)(object)text;
            }

            var result = await response.Content.ReadFromJsonAsync<TResult>(_jsonOptions, cancellationToken);
            return result!;
        }

        /// <summary>Consome respostas JSON-array incrementalmente (server-side IAsyncEnumerable).</summary>
        private async IAsyncEnumerable<TItem> InvokeStreamAsync<TItem>(MethodInfo method, object?[] args)
        {
            using var request = BuildRequest(method, args, out var cancellationToken);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<TItem>(stream, _jsonOptions, cancellationToken))
            {
                yield return item!;
            }
        }

        private HttpRequestMessage BuildRequest(MethodInfo method, object?[] args, out CancellationToken cancellationToken)
        {
            if (_httpClient.BaseAddress is null)
            {
                throw new InvalidOperationException(
                    $"O HttpClient usado pelo DynamicHttpProxy<{typeof(T).Name}> não tem BaseAddress configurado. " +
                    "Defina httpClient.BaseAddress (ex.: no configureClient do AddAutoApiClient) antes da primeira chamada.");
            }

            cancellationToken = CancellationToken.None;

            var httpMethod = ApiRouteBuilder.GetHttpMethod(method.Name);
            var actionName = ApiRouteBuilder.GetActionName(method.Name, _routeOptions);
            var route = ApiRouteBuilder.GetApiServiceRouteFromInterface(typeof(T).Name, actionName, _routeOptions);

            var allowsBody = !NoBodyHttpMethods.Contains(httpMethod, StringComparer.OrdinalIgnoreCase);

            var parameters = method.GetParameters();
            var query = new List<string>();
            object? bodyValue = null;
            RemoteStreamContent? streamValue = null;
            string? streamParameterName = null;

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var value = args[i];

                if (parameter.ParameterType == typeof(CancellationToken))
                {
                    cancellationToken = value as CancellationToken? ?? CancellationToken.None;
                    continue;
                }

                if (TypeHelper.IsPrimitiveExtended(parameter.ParameterType, includeEnums: true))
                {
                    AppendQueryParameter(query, parameter.Name!, value);
                }
                else if (parameter.ParameterType == typeof(RemoteStreamContent))
                {
                    if (!allowsBody)
                    {
                        throw new NotSupportedException(
                            $"'{method.Name}': upload (RemoteStreamContent) requer um verbo com corpo (POST/PUT/PATCH).");
                    }

                    streamValue = value as RemoteStreamContent;
                    streamParameterName = parameter.Name!;
                }
                else if (allowsBody)
                {
                    if (bodyValue != null)
                    {
                        throw new NotSupportedException(
                            $"'{method.Name}': mais de um parâmetro complexo (corpo) não é suportado.");
                    }

                    // Tipo complexo em verbo com corpo -> JSON body.
                    bodyValue = value;
                }
                else
                {
                    // Tipo complexo em GET/DELETE -> espalha as propriedades simples na query string.
                    AppendObjectToQuery(query, value);
                }
            }

            if (streamValue != null && bodyValue != null)
            {
                throw new NotSupportedException(
                    $"'{method.Name}': não é possível combinar upload (RemoteStreamContent) com outro parâmetro de corpo.");
            }

            var url = query.Count > 0 ? $"{route}?{string.Join("&", query)}" : route;

            var request = new HttpRequestMessage(new HttpMethod(httpMethod), url);

            if (streamValue != null)
            {
                var multipart = new MultipartFormDataContent();
                var streamContent = new StreamContent(streamValue.Stream);
                if (streamValue.ContentType != null)
                {
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(streamValue.ContentType);
                }

                multipart.Add(streamContent, streamParameterName!, streamValue.FileName ?? streamParameterName!);
                request.Content = multipart;
            }
            else if (allowsBody && bodyValue != null)
            {
                request.Content = JsonContent.Create(bodyValue, bodyValue.GetType(), mediaType: null, _jsonOptions);
            }

            return request;
        }

        private static void AppendQueryParameter(List<string> query, string name, object? value)
        {
            if (value == null)
            {
                return;
            }

            query.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(FormatSimpleValue(value))}");
        }

        /// <summary>
        /// Espalha as propriedades públicas simples (e coleções de simples) de um DTO na query
        /// string. LIMITAÇÃO: achata apenas 1 nível — propriedades complexas aninhadas (e coleções
        /// dentro delas) não têm representação canônica em query string e são omitidas.
        /// </summary>
        private static void AppendObjectToQuery(List<string> query,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] object? value)
        {
            if (value == null)
            {
                return;
            }

            foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead)
                {
                    continue;
                }

                var propertyValue = property.GetValue(value);
                if (propertyValue == null)
                {
                    continue;
                }

                if (TypeHelper.IsPrimitiveExtended(property.PropertyType, includeEnums: true))
                {
                    AppendQueryParameter(query, property.Name, propertyValue);
                }
                else if (propertyValue is System.Collections.IEnumerable enumerable)
                {
                    // Expande coleções em múltiplos parâmetros com o mesmo nome
                    // (?tags=a&tags=b), que o model binding do ASP.NET Core entende.
                    foreach (var item in enumerable)
                    {
                        AppendQueryParameter(query, property.Name, item);
                    }
                }
                // Propriedades complexas não-enumeráveis em GET/DELETE não têm representação
                // canônica em query string e são omitidas deliberadamente (documentado acima).
            }
        }

        // Contrato de serialização em query string: datas/horas em ISO-8601 invariante ("O"),
        // enums pelo NOME (o model binding do ASP.NET Core aceita nomes case-insensitive).
        private static string FormatSimpleValue(object value)
        {
            return value switch
            {
                bool b => b ? "true" : "false",
                DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
                DateOnly d => d.ToString("O", CultureInfo.InvariantCulture),
                TimeOnly t => t.ToString("O", CultureInfo.InvariantCulture),
                Enum e => e.ToString(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
        }
    }
}
