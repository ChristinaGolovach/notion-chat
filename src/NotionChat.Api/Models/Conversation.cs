using Anthropic.Models.Messages;

namespace NotionChat.Api.Models;

public class Conversation
{
	public string Id { get; set; } = Guid.NewGuid().ToString();
	public List<MessageParam> Messages { get; set; } = [];
	public string SystemPrompt { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
