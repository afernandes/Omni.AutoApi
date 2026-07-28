using Omni.AutoApi.AspNetCore;
using Omni.AutoApi.Client;
using Omni.AutoApi.Sample.Web.Auth;
using Omni.AutoApi.Sample.Web.Services;
using Scalar.AspNetCore;

namespace Omni.AutoApi.Sample.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Lado servidor: expõe os Application Services (IRemoteService) como controllers.
            builder.Services.AddAutoApiServices();

            // JWT Bearer + policy "todo:admin" — [Authorize] funciona normalmente nos
            // Application Services (o metadata é preservado pela convenção).
            builder.Services.AddSampleJwtAuth();

            builder.Services.AddOpenApi();

            // Lado cliente (opcional). Duas formas equivalentes de obter um ITodoAppService:
            //
            // (1) Proxy DINÂMICO (runtime, Omni.AutoApi.Client) — sem geração de código:
            //        builder.Services.AddAutoApiClient<ITodoAppService>((_, c) => c.BaseAddress = ...);
            //
            // (2) Cliente GERADO (compile-time, Omni.AutoApi.Client.SourceGenerator) — typed client:
            //        builder.Services.AddHttpClient<ITodoAppService, TodoAppServiceClient>(c => c.BaseAddress = ...);
            //
            // A URL base vem de "RemoteServices:Default:BaseUrl"; sem ela, nada é registrado.
            var remoteBaseUrl = builder.Configuration["RemoteServices:Default:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(remoteBaseUrl))
            {
                builder.Services.AddAutoApiClient<ITodoAppService>((_, client) =>
                {
                    client.BaseAddress = new Uri(remoteBaseUrl);
                });
            }

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "Auto Api Controller");
            });

            app.MapScalarApiReference("/scalar", options =>
            {
                options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            });

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            // Emissor de token só do sample (num sistema real seria o identity provider).
            app.MapPost("/dev/token", (TokenRequest req) =>
                Results.Ok(new { access_token = JwtSetup.CreateToken(req.User, req.Roles ?? []) }))
               .WithSummary("Gera um JWT de teste (apenas para o sample).");

            // Endpoint de definição da API (estilo ABP /api/abp/api-definition).
            app.MapAutoApiDefinition();

            app.Run();
        }
    }

    /// <summary>Corpo do emissor de token do sample.</summary>
    public record TokenRequest(string User, string[]? Roles);
}
