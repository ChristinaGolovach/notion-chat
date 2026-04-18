using System.Collections.Concurrent;
using Anthropic.Models.Messages;
using NotionChat.Api.Models;

namespace NotionChat.Api.Services;

public class ConversationManager
{
	private const int MaxMessages = 50;

	private readonly ConcurrentDictionary<string, Conversation> _conversations = new();
	private readonly PromptBuilder _promptBuilder;

	public ConversationManager(PromptBuilder promptBuilder)
	{
		_promptBuilder = promptBuilder;
	}

	public Conversation GetOrCreate(string conversationId = null)
	{
		if (conversationId != null && _conversations.TryGetValue(conversationId, out var existing))
			return existing;

		var conversation = new Conversation
		{
			SystemPrompt = _promptBuilder.Build()
		};

		_conversations[conversation.Id] = conversation;
		return conversation;
	}

	public Conversation Get(string conversationId)
	{
		return _conversations.TryGetValue(conversationId, out var conversation)
			? conversation
			: throw new KeyNotFoundException($"Conversation '{conversationId}' not found.");
	}

	public void AddUserMessage(string conversationId, string text)
	{
		var conversation = Get(conversationId);
		conversation.Messages.Add(new MessageParam
		{
			Role = Role.User,
			Content = text
		});
		TrimIfNeeded(conversation);
	}

	public void AddAssistantMessage(string conversationId, List<ContentBlock> content)
	{
		var conversation = Get(conversationId);
		var blocks = new List<ContentBlockParam>();
		foreach (var block in content)
		{
			if (block.TryPickText(out var text))
				blocks.Add(new TextBlockParam { Text = text.Text });
			else if (block.TryPickToolUse(out var toolUse))
				blocks.Add(new ToolUseBlockParam { ID = toolUse.ID, Name = toolUse.Name, Input = toolUse.Input });
		}
		conversation.Messages.Add(new MessageParam
		{
			Role = Role.Assistant,
			Content = blocks
		});
		TrimIfNeeded(conversation);
	}

	public void AddToolResult(string conversationId, string toolUseId, string result)
	{
		var conversation = Get(conversationId);
		conversation.Messages.Add(new MessageParam
		{
			Role = Role.User,
			Content = new List<ContentBlockParam>
			{
				new ToolResultBlockParam
				{
					ToolUseID = toolUseId,
					Content = result
				}
			}
		});
	}

	private void TrimIfNeeded(Conversation conversation)
	{
		while (conversation.Messages.Count > MaxMessages)
		{
			conversation.Messages.RemoveAt(0);
		}
	}
}
