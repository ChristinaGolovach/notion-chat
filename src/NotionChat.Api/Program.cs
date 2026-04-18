using ModelContextProtocol.Protocol;
using NotionChat.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<ClaudeApiClient>();
builder.Services.AddSingleton<PromptBuilder>();
builder.Services.AddSingleton<ConversationManager>();
builder.Services.AddSingleton<McpToolRouter>();
builder.Services.AddSingleton<AgentOrchestrator>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

//app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

// Initialize the agent orchestrator (starts MCP server, caches tools)
var orchestrator = app.Services.GetRequiredService<AgentOrchestrator>();
await orchestrator.InitializeAsync();

app.Run();
