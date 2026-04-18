using System.Text.Json;
using Anthropic.Models.Messages;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace NotionChat.Api.Services;

using Tool = Anthropic.Models.Messages.Tool;

public class AgentOrchestrator : IAsyncDisposable
{
	private const int MaxIterations = 10;

	private readonly McpToolRouter _mcpRouter;
	private readonly ClaudeApiClient _claudeClient;
	private readonly ConversationManager _conversationManager;
	private readonly ILogger<AgentOrchestrator> _logger;

	private IList<McpClientTool> _mcpTools = [];
	private List<ToolUnion> _claudeTools = [];

	public AgentOrchestrator(
		McpToolRouter mcpRouter,
		ClaudeApiClient claudeClient,
		ConversationManager conversationManager,
		ILogger<AgentOrchestrator> logger)
	{
		_mcpRouter = mcpRouter;
		_claudeClient = claudeClient;
		_conversationManager = conversationManager;
		_logger = logger;
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		await _mcpRouter.InitializeAsync(cancellationToken);
		_mcpTools = _mcpRouter.GetToolDefinitions();

		_claudeTools = _mcpTools.Select(t => (ToolUnion)new Tool
		{
			Name = t.Name,
			Description = t.Description ?? string.Empty,
			InputSchema = JsonSerializer.Deserialize<InputSchema>(
				t.JsonSchema.GetRawText()) ?? new InputSchema()
		}).ToList();

		_logger.LogInformation("AgentOrchestrator initialized with {ToolCount} tools", _claudeTools.Count);
	}

	public async Task<string> ProcessMessageAsync(
		string conversationId,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		_conversationManager.AddUserMessage(conversationId, userMessage);
		var conversation = _conversationManager.Get(conversationId);

		for (int iteration = 0; iteration < MaxIterations; iteration++)
		{
			_logger.LogInformation("Iteration {Iteration}: sending {MessageCount} messages to Claude",
				iteration + 1, conversation.Messages.Count);

			var request = new MessageCreateParams
			{
				Model = Model.ClaudeSonnet4_5,
				MaxTokens = 4096,
				System = conversation.SystemPrompt,
				Messages = conversation.Messages,
				Tools = _claudeTools
			};

			var response = await _claudeClient.SendMessageAsync(request, cancellationToken);

			_conversationManager.AddAssistantMessage(conversationId, response.Content.ToList());

			if (response.StopReason != "tool_use")
			{
				var textContent = string.Join("", response.Content
					.Where(b => b.TryPickText(out _))
					.Select(b => { b.TryPickText(out var t); return t!.Text; }));

				_logger.LogInformation("Final response received (iteration {Iteration})", iteration + 1);
				return textContent;
			}

			foreach (var block in response.Content)
			{
				if (!block.TryPickToolUse(out var toolUse))
					continue;

				_logger.LogInformation("Tool call: {ToolName} (id: {ToolId})", toolUse.Name, toolUse.ID);

				string toolResult;
				try
				{
					var arguments = toolUse.Input
						.ToDictionary(kv => kv.Key, kv => (object)kv.Value.ToString()!);

					var mcpResult = await _mcpRouter.ExecuteToolAsync(
						toolUse.Name, arguments, cancellationToken);

					toolResult = string.Join("\n", mcpResult.Content
						.OfType<TextContentBlock>()
						.Select(t => t.Text));
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Tool {ToolName} failed", toolUse.Name);
					toolResult = $"Error executing tool '{toolUse.Name}': {ex.Message}";
				}

				_conversationManager.AddToolResult(conversationId, toolUse.ID, toolResult);
			}
		}

		_logger.LogWarning("Max iterations ({MaxIterations}) reached", MaxIterations);
		return "I'm sorry, I wasn't able to complete the request within the allowed number of steps. Please try rephrasing your question.";
	}

	public async ValueTask DisposeAsync()
	{
		await _mcpRouter.DisposeAsync();
		GC.SuppressFinalize(this);
	}
}
