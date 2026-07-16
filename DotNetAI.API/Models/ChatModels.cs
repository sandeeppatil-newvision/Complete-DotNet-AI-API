namespace DotNetAI.API.Models
{
    public class ChatModels
    {
        public record ChatRequest(string Message);
        public record ChatResponse(string Reply, string Provider);
    }
}
