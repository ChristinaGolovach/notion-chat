using Microsoft.AspNetCore.Mvc;
using NotionChat.Api.Services;

namespace NotionChat.Api.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
	private readonly AgentOrchestrator _orchestrator;
	private readonly ConversationManager _conversationManager;

	public ChatController(AgentOrchestrator orchestrator, ConversationManager conversationManager)
	{
		_orchestrator = orchestrator;
		_conversationManager = conversationManager;
	}

	[HttpPost("conversations")]
	public IActionResult CreateConversation()
	{
		var conversation = _conversationManager.GetOrCreate();
		return Ok(new { conversationId = conversation.Id });
	}

	[HttpPost("conversations/{id}/messages")]
	public async Task<IActionResult> SendMessage(string id, [FromBody] SendMessageRequest request, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Message))
			return BadRequest(new { error = "Message is required." });

		try
		{
			_ = _conversationManager.Get(id);
		}
		catch (KeyNotFoundException)
		{
			return NotFound(new { error = $"Conversation '{id}' not found." });
		}

		var reply = await _orchestrator.ProcessMessageAsync(id, request.Message, cancellationToken);
		return Ok(new { reply });
	}
}

public class SendMessageRequest
{
	public string Message { get; set; } = string.Empty;
}
