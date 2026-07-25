using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Omni.AutoApi.AspNetCore
{
    /// <summary>
    /// Materializa um <see cref="RemoteStreamContent"/> no servidor a partir de
    /// <c>multipart/form-data</c> (form file com o nome do parâmetro, ou o primeiro arquivo)
    /// ou, na ausência de form, do corpo bruto da requisição — análogo ao binding do
    /// <c>IRemoteStreamContent</c> do ABP.
    /// </summary>
    internal sealed class RemoteStreamContentModelBinder : IModelBinder
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var request = bindingContext.HttpContext.Request;

            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(bindingContext.HttpContext.RequestAborted);
                var file = form.Files.GetFile(bindingContext.FieldName)
                    ?? form.Files.GetFile(bindingContext.ModelName)
                    ?? (form.Files.Count > 0 ? form.Files[0] : null);

                if (file != null)
                {
                    bindingContext.Result = ModelBindingResult.Success(
                        new RemoteStreamContent(file.OpenReadStream(), file.FileName, file.ContentType, file.Length));
                    return;
                }
            }
            else if (request.ContentLength > 0 || request.Headers.ContainsKey("Transfer-Encoding"))
            {
                bindingContext.Result = ModelBindingResult.Success(
                    new RemoteStreamContent(request.Body, fileName: null, request.ContentType, request.ContentLength));
                return;
            }

            bindingContext.Result = ModelBindingResult.Failed();
        }
    }

    internal sealed class RemoteStreamContentModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            return context.Metadata.ModelType == typeof(RemoteStreamContent)
                ? new RemoteStreamContentModelBinder()
                : null;
        }
    }
}
