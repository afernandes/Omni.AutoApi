using System.Net.Http.Headers;

namespace Omni.AutoApi.Client
{
    /// <summary>
    /// <see cref="DelegatingHandler"/> que anexa <c>Authorization: Bearer …</c> às chamadas dos
    /// clientes Auto API. O token é obtido por callback a cada requisição — assim funciona tanto
    /// com token fixo quanto com renovação (basta o callback devolver o token atual).
    /// <para>
    /// Encadeie no <c>IHttpClientBuilder</c> devolvido pelo registro do cliente:
    /// <code>
    /// services.AddSingleton(new AuthTokenHandler(sp => ObterToken()));
    /// services.AddTodoAppServiceClient((_, c) => c.BaseAddress = url)
    ///         .AddHttpMessageHandler&lt;AuthTokenHandler&gt;();
    /// </code>
    /// </para>
    /// </summary>
    public class AuthTokenHandler : DelegatingHandler
    {
        private readonly Func<CancellationToken, ValueTask<string?>> _tokenProvider;

        public AuthTokenHandler(Func<string?> tokenProvider)
        {
            ArgumentNullException.ThrowIfNull(tokenProvider);
            _tokenProvider = _ => new ValueTask<string?>(tokenProvider());
        }

        public AuthTokenHandler(Func<CancellationToken, ValueTask<string?>> tokenProvider)
        {
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Não sobrescreve um Authorization já definido explicitamente na requisição.
            if (request.Headers.Authorization is null)
            {
                var token = await _tokenProvider(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
