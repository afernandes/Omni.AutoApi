using Omni.AutoApi.Client.Generated;
using Microsoft.Extensions.Logging;
using SharedApp.Shared.Services;
using SharedApp.Services;

// API remota consumida pelo MAUI. Ajuste para o seu servidor:
//   Windows/desktop:  https://localhost:7xxx   |   Android emulator: http://10.0.2.2:5xxx

namespace SharedApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Add device-specific services used by the SharedApp.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();

        // Omni.AutoApi: a MESMA extensão gerada usada pelo WASM, apontando para o servidor remoto.
        builder.Services.AddAllAutoApiClients((_, http) =>
            http.BaseAddress = new Uri("https://localhost:7178/"));

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
