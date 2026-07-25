namespace Omni.AutoApi.Sample.Web.Contracts
{
    public class UpdateTodoDto
    {
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
    }
}
