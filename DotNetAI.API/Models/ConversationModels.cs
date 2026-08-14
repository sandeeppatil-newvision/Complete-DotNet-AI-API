namespace DotNetAI.API.Models
{
    public class ConversationModels
    {
        public record ConversationRequest(
            string SessionId,   // uniquely identifies one conversation
            string Message);    // what the user is asking this turn

        public record ConversationResponse(
            string SessionId,
            string Reply,
            int TurnCount,  // messages in this session so far
            string Provider);

        public record ConversationSummary(
            string SessionId,
            int TurnCount,
            string LastMessage);
    }
}
