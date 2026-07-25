using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Omni.AutoApi.AspNetCore
{
    /// <summary>
    /// Roda logo APÓS o <c>DefaultApiDescriptionProvider</c> (Order = -1000) e apenas
    /// ENRIQUECE as descrições reais geradas pelo pipeline MVC — sem recriá-las. No estilo
    /// do <c>AbpNoContentApiDescriptionProvider</c>, troca o 200 inferido por 204 quando a
    /// ação retorna void/Task/ValueTask.
    /// </summary>
    public class AutoApiResponseEnrichmentProvider : IApiDescriptionProvider
    {
        public int Order => -999;

        public void OnProvidersExecuting(ApiDescriptionProviderContext context)
        {
            foreach (var description in context.Results)
            {
                if (description.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
                {
                    continue;
                }

                if (!AutoApiHelper.IsAutoApiController(actionDescriptor.ControllerTypeInfo))
                {
                    continue;
                }

                EnrichNoContentResponse(description, actionDescriptor);
            }
        }

        public void OnProvidersExecuted(ApiDescriptionProviderContext context)
        {
        }

        private static void EnrichNoContentResponse(ApiDescription description, ControllerActionDescriptor actionDescriptor)
        {
            var returnType = UnwrapTask(actionDescriptor.MethodInfo.ReturnType);
            if (returnType != typeof(void))
            {
                return;
            }

            for (var i = description.SupportedResponseTypes.Count - 1; i >= 0; i--)
            {
                if (description.SupportedResponseTypes[i].StatusCode == StatusCodes.Status200OK)
                {
                    description.SupportedResponseTypes.RemoveAt(i);
                }
            }

            if (description.SupportedResponseTypes.All(r => r.StatusCode != StatusCodes.Status204NoContent))
            {
                description.SupportedResponseTypes.Add(new ApiResponseType
                {
                    StatusCode = StatusCodes.Status204NoContent,
                    Type = typeof(void)
                });
            }
        }

        private static Type UnwrapTask(Type type)
        {
            if (type == typeof(Task) || type == typeof(ValueTask))
            {
                return typeof(void);
            }

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                if (definition == typeof(Task<>) || definition == typeof(ValueTask<>))
                {
                    return type.GetGenericArguments()[0];
                }
            }

            return type;
        }
    }
}
