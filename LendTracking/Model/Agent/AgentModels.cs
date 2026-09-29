namespace LendTracking.Model.Agent
{
    public class ChatMessageDto
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    public class AgentChatRequest
    {
        public List<ChatMessageDto> Messages { get; set; } = [];

        /// <summary>English display name (e.g. "Telugu") the client has pinned for this conversation,
        /// from an explicit language picker or a confirmed voice-transcription language. When set, the
        /// model is told to reply in this language regardless of the script of any individual message.</summary>
        public string? LanguageHint { get; set; }
    }

    public class PendingAction
    {
        public required string Action { get; set; }
        public required string PayloadJson { get; set; }
        public required int UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
