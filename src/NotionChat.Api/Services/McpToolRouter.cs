using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace NotionChat.Api.Services;

public class McpToolRouter : IAsyncDisposable
{
	private readonly string _notionApiKey;
	private readonly ILogger<McpToolRouter> _logger;
	private McpClient _client;
	private IList<McpClientTool> _tools;

	public McpToolRouter(IConfiguration configuration, ILogger<McpToolRouter> logger)
	{
		_notionApiKey = configuration["Notion:ApiKey"]
			?? throw new InvalidOperationException("Notion:ApiKey is not configured.");
		_logger = logger;
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Starting Notion MCP server...");

		var transport = new StdioClientTransport(new StdioClientTransportOptions
		{
			Name = "NotionMcpServer",
			Command = "npx",
			Arguments = ["-y", "@notionhq/notion-mcp-server"],
			EnvironmentVariables = new Dictionary<string, string>
			{
				["OPENAPI_MCP_HEADERS"] =
					$"{{\"Authorization\":\"Bearer {_notionApiKey}\",\"Notion-Version\":\"2022-06-28\"}}"
			}
		});

		_client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
		_tools = await _client.ListToolsAsync(cancellationToken: cancellationToken);

		_logger.LogInformation("Notion MCP server started. {ToolCount} tools available: {ToolNames}",
			_tools.Count, string.Join(", ", _tools.Select(t => t.Name)));
	}

	public IList<McpClientTool> GetToolDefinitions()
	{
		return _tools ?? throw new InvalidOperationException("McpToolRouter not initialized. Call InitializeAsync first.");
	}

	public async Task<CallToolResult> ExecuteToolAsync(
		string toolName,
		IReadOnlyDictionary<string, object> arguments = null,
		CancellationToken cancellationToken = default)
	{
		if (_client is null)
			throw new InvalidOperationException("McpToolRouter not initialized. Call InitializeAsync first.");

		_logger.LogInformation("Executing MCP tool: {ToolName}", toolName);

		var result = await _client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken);

		if (result.IsError is true)
		{
			_logger.LogWarning("MCP tool {ToolName} returned error", toolName);
		}

		return result;
	}

	public async ValueTask DisposeAsync()
	{
		if (_client is not null)
		{
			_logger.LogInformation("Shutting down Notion MCP server...");
			await _client.DisposeAsync();
			_client = null;
		}

		GC.SuppressFinalize(this);
	}
}
