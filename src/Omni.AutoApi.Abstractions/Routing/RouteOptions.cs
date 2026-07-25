using System;

namespace Omni.AutoApi.Routing
{
    /// <summary>
    /// Opções de convenção de rota, análogas (em escopo reduzido) ao
    /// <c>AbpConventionalControllerOptions</c> do ABP. Usadas pelo servidor, pelo proxy
    /// dinâmico e pelo source generator para manterem a MESMA derivação de rota.
    /// </summary>
    public sealed class RouteOptions
    {
        /// <summary>Prefixo de todas as rotas (padrão "api/app-service").</summary>
        public string Prefix { get; set; } = "api/app-service";

        /// <summary>Sufixos removidos do nome do serviço ao formar o nome do controller.</summary>
        public string[] ControllerPostfixes { get; set; } =
        {
            "ApplicationService",
            "AppService",
            "Service",
            "Controller"
        };

        /// <summary>true = kebab-case (padrão); false = camelCase (estilo legado ABP v3).</summary>
        public bool UseKebabCase { get; set; } = true;

        /// <summary>Instância padrão (preserva o comportamento histórico).</summary>
        public static readonly RouteOptions Default = new();
    }
}
