using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using BelfastBinsApi.Models;
using System.Collections.Concurrent;

namespace BelfastBinsApi.Controllers;

[ApiController]
[Route("api")]
public class ChatController : ControllerBase
{
    private readonly Kernel _kernel;
    private readonly ILogger<ChatController> _logger;
    private static readonly ConcurrentDictionary<string, ChatHistory> _sessions = new();

    private const string SystemPrompt = @"You are WhatBin, a friendly and helpful assistant for Belfast bin collections. 
You help residents of Belfast find out when their bins are being collected.

You have access to the Belfast City Council bin collection database. When users ask about their bin collections, 
use the available functions to look up their schedule.

Key information:
- Belfast has 4 bin types: Black (general waste), Blue (recycling), Brown (garden waste), and Glass (glass recycling)
- Collections alternate on a 2-week cycle (Week A / Week B)
- You can also provide recycling guidance about what goes in each bin

When a user provides their postcode (and optionally house number), look up their collection schedule.
If they ask about a specific bin type, use the targeted lookup function.
If they ask what goes in a bin, use the recycling guidance function.

Be concise, friendly, and helpful. Use plain language. If you don't know something, say so.
Always mention the specific collection date when providing schedule information.";

    public ChatController(Kernel kernel, ILogger<ChatController> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { detail = "Message is required" });
            }

            var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

            var history = _sessions.GetOrAdd(sessionId, _ =>
            {
                var h = new ChatHistory();
                h.AddSystemMessage(SystemPrompt);

                // If postcode is provided in the request, add context
                if (!string.IsNullOrEmpty(request.Postcode))
                {
                    h.AddSystemMessage($"The user's postcode is {request.Postcode}" +
                        (string.IsNullOrEmpty(request.HouseNumber) ? "" : $" and their house number is {request.HouseNumber}") +
                        ". Use this information when looking up their bin collections.");
                }

                return h;
            });

            // If postcode provided on subsequent messages, update context
            if (!string.IsNullOrEmpty(request.Postcode) && history.Count > 1)
            {
                var contextMsg = $"The user's postcode is {request.Postcode}" +
                    (string.IsNullOrEmpty(request.HouseNumber) ? "" : $" and their house number is {request.HouseNumber}");
                // Check if we already have this context to avoid duplication
                var existingContext = history.FirstOrDefault(m =>
                    m.Role == AuthorRole.System && m.Content != null && m.Content.Contains("postcode is"));
                if (existingContext != null)
                {
                    history.Remove(existingContext);
                }
                history.Insert(1, new ChatMessageContent(AuthorRole.System, contextMsg + ". Use this information when looking up their bin collections."));
            }

            history.AddUserMessage(request.Message);

            _logger.LogInformation("Chat request - Session: {SessionId}, Message: {Message}", sessionId, request.Message);

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            var executionSettings = new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var response = await chatService.GetChatMessageContentAsync(
                history,
                executionSettings,
                _kernel);

            var reply = response.Content ?? "I'm sorry, I couldn't generate a response. Please try again.";

            history.AddAssistantMessage(reply);

            // Limit history size to prevent token overflow
            while (history.Count > 20)
            {
                // Remove oldest non-system messages
                var firstNonSystem = history.FirstOrDefault(m => m.Role != AuthorRole.System);
                if (firstNonSystem != null)
                {
                    history.Remove(firstNonSystem);
                }
                else
                {
                    break;
                }
            }

            return Ok(new ChatResponse
            {
                Reply = reply,
                SessionId = sessionId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return StatusCode(500, new { detail = $"Error processing chat: {ex.Message}" });
        }
    }

    [HttpDelete("chat/{sessionId}")]
    public IActionResult ClearSession(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        return Ok(new { message = "Session cleared" });
    }
}
