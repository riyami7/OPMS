using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OperationalPlanMS.Services.AI;
using OperationalPlanMS.Services.AI.Models;

namespace OperationalPlanMS.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IOllamaService _ollama;
        private readonly ChatContextBuilder _contextBuilder;
        private readonly OllamaSettings _settings;

        public ChatController(
            IOllamaService ollama,
            ChatContextBuilder contextBuilder,
            IOptions<OllamaSettings> settings)
        {
            _ollama = ollama;
            _contextBuilder = contextBuilder;
            _settings = settings.Value;
        }

        // ═══════════════════════════════════════════════════════════
        //  POST /Chat/Stream — SSE streaming endpoint
        // ═══════════════════════════════════════════════════════════

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task Stream([FromBody] ChatRequestDto dto, CancellationToken cancellationToken)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            try
            {
                // Build system prompt with OPMS context
                var systemPrompt = await _contextBuilder.BuildSystemPromptAsync(
                    User, _settings.SystemPrompt);

                // Build messages list
                var messages = new List<OllamaChatMessage>
                {
                    new() { Role = "system", Content = systemPrompt }
                };

                // Add conversation history (last 10 messages max)
                if (dto.History?.Count > 0)
                {
                    var historyStart = Math.Max(0, dto.History.Count - 10);
                    for (int i = historyStart; i < dto.History.Count; i++)
                    {
                        messages.Add(new OllamaChatMessage
                        {
                            Role = dto.History[i].Role,
                            Content = dto.History[i].Content
                        });
                    }
                }

                // Add current message
                messages.Add(new OllamaChatMessage
                {
                    Role = "user",
                    Content = dto.Message
                });

                var request = new OllamaChatRequest
                {
                    Model = dto.Model ?? _settings.DefaultModel,
                    Messages = messages,
                    Stream = true,
                    Options = new OllamaOptions
                    {
                        Temperature = _settings.Temperature
                    }
                };

                // Stream tokens via SSE
                await foreach (var token in _ollama.ChatStreamAsync(request, cancellationToken))
                {
                    // SSE format: data: {json}\n\n
                    var escaped = token.Replace("\\", "\\\\").Replace("\"", "\\\"")
                                       .Replace("\n", "\\n").Replace("\r", "\\r");
                    await Response.WriteAsync($"data: {{\"token\":\"{escaped}\"}}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }

                // Send done signal
                await Response.WriteAsync("data: {\"done\":true}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Client disconnected — normal
            }
            catch (Exception ex)
            {
                var errorMsg = "عذراً، حدث خطأ في الاتصال بالذكاء الاصطناعي.";
                await Response.WriteAsync($"data: {{\"error\":\"{errorMsg}\"}}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  GET /Chat/Models — List available models
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> Models(CancellationToken cancellationToken)
        {
            var models = await _ollama.ListModelsAsync(cancellationToken);
            return Json(models);
        }

        // ═══════════════════════════════════════════════════════════
        //  GET /Chat/Status — Health check
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> Status(CancellationToken cancellationToken)
        {
            var available = await _ollama.IsAvailableAsync(cancellationToken);
            return Json(new
            {
                available,
                model = _settings.DefaultModel,
                baseUrl = _settings.BaseUrl
            });
        }

        // GET /Chat/DebugContext — Temporary: see what context the AI receives

        [HttpGet]
        public async Task<IActionResult> DebugContext(CancellationToken cancellationToken)
        {
            var username = User.Identity?.Name;
            var isAuth = User.Identity?.IsAuthenticated;
            var claims = User.Claims.Select(c => $"{c.Type} = {c.Value}").ToList();

            var systemPrompt = await _contextBuilder.BuildSystemPromptAsync(
                User, _settings.SystemPrompt);

            var debug = $"Username: {username}\nIsAuthenticated: {isAuth}\nClaims:\n{string.Join("\n", claims)}\n\n---PROMPT---\n{systemPrompt}";
            return Content(debug, "text/plain; charset=utf-8");
        }
    }
}
