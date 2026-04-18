using Anthropic;
using Anthropic.Models.Messages;

namespace NotionChat.Api.Services;

public class ClaudeApiClient
{
	private readonly AnthropicClient _client;
	private readonly ILogger<ClaudeApiClient> _logger;

	public ClaudeApiClient(IConfiguration configuration, ILogger<ClaudeApiClient> logger)
	{
		_logger = logger;

		var apiKey = configuration["Claude:ApiKey"]
			?? throw new InvalidOperationException("Claude:ApiKey is not configured.");

		_client = new AnthropicClient { ApiKey = apiKey };
	}

	public async Task<Message> SendMessageAsync(MessageCreateParams request, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Sending request to Claude API. Model: {Model}, Messages: {Count}",
			request.Model, request.Messages.Count);

		var response = await _client.Messages.Create(request, cancellationToken: cancellationToken);

		_logger.LogDebug("Claude API response: StopReason={StopReason}, Usage: in={InputTokens} out={OutputTokens}",
			response.StopReason, response.Usage.InputTokens, response.Usage.OutputTokens);

		return response;
	}
}
