using System.ComponentModel.DataAnnotations;

namespace Omni.AutoApi.Sample.Web.Contracts
{
    public class CreateTodoDto
    {
        // Exercita a validação automática: ausente/vazio -> 400 ProblemDetails (ValidationError).
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public bool IsCompleted { get; set; } = false;
    }
}
