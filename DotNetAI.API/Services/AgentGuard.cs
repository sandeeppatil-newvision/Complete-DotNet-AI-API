namespace DotNetAI.API.Services
{
    public static class AgentGuard
    {
        public const int MaxMessages = 20;
        public const int MaxCharsPerMessage = 2000;
        public const int MaxTotalChars = 8000;

        // Returns an error message, or null when the request is acceptable.
        public static string? Validate(IReadOnlyList<AgentService.AgentMessage>? messages)
        {
            if (messages is null || messages.Count == 0) return "No messages.";
            if (messages.Count > MaxMessages) return $"Too many messages (max {MaxMessages}).";

            var total = 0;
            foreach (var m in messages)
            {
                var role = m.Role?.Trim().ToLowerInvariant();
                if (role is not ("user" or "assistant"))
                    return "Only 'user' and 'assistant' roles are allowed.";

                var length = m.Content?.Length ?? 0;
                if (length > MaxCharsPerMessage)
                    return $"A message is too long (max {MaxCharsPerMessage} characters).";
                total += length;
            }

            if (total > MaxTotalChars) return $"Conversation too long (max {MaxTotalChars} characters).";
            if (!string.Equals(messages[^1].Role, "user", StringComparison.OrdinalIgnoreCase))
                return "The last message must come from the user.";

            return null;
        }
    }
}
