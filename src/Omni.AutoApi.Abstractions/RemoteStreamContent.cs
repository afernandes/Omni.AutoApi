namespace Omni.AutoApi
{
    /// <summary>
    /// Abstração de conteúdo binário para upload/download em serviços remotos — análoga ao
    /// <c>IRemoteStreamContent</c> do ABP. Permite que a MESMA interface de contrato declare
    /// upload de arquivo sem depender de <c>IFormFile</c> (tipo do servidor ASP.NET Core):
    /// no cliente (gerado ou dinâmico) vira <c>multipart/form-data</c>; no servidor, um
    /// model binder dedicado materializa a instância a partir do form file ou do corpo.
    /// </summary>
    public class RemoteStreamContent
    {
        public RemoteStreamContent(Stream stream, string? fileName = null, string? contentType = null, long? length = null)
        {
            Stream = stream ?? throw new System.ArgumentNullException(nameof(stream));
            FileName = fileName;
            ContentType = contentType;
            Length = length;
        }

        /// <summary>Conteúdo. O dono do stream é o chamador (cliente) ou a requisição (servidor).</summary>
        public Stream Stream { get; }

        public string? FileName { get; }

        public string? ContentType { get; }

        public long? Length { get; }
    }
}
