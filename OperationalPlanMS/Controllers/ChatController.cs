using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models.Entities;
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
        private readonly AppDbContext _db;

        public ChatController(
            IOllamaService ollama,
            ChatContextBuilder contextBuilder,
            IOptions<OllamaSettings> settings,
            AppDbContext db)
        {
            _ollama = ollama;
            _contextBuilder = contextBuilder;
            _settings = settings.Value;
            _db = db;
        }

        // ═══════════════════════════════════════════════════════════
        //  GET /Chat — Full chat page with sidebar
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // ═══════════════════════════════════════════════════════════
        //  POST /Chat/Stream — SSE streaming endpoint + DB save
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
                // ─── 1. Resolve current user ID ───
                int? currentUserId = GetCurrentUserId();

                // ─── 2. Get or create conversation ───
                ChatConversation conversation = null;

                if (dto.ConversationId.HasValue && dto.ConversationId.Value > 0)
                {
                    // Load existing conversation (verify ownership)
                    conversation = await _db.ChatConversations
                        .FirstOrDefaultAsync(c => c.Id == dto.ConversationId.Value
                            && c.UserId == currentUserId
                            && !c.IsDeleted, cancellationToken);
                }

                if (conversation == null && currentUserId.HasValue)
                {
                    // Create new conversation
                    conversation = new ChatConversation
                    {
                        UserId = currentUserId.Value,
                        Title = dto.Message.Length > 100
                            ? dto.Message.Substring(0, 100) + "..."
                            : dto.Message,
                        CreatedAt = DateTime.Now,
                        LastMessageAt = DateTime.Now
                    };
                    _db.ChatConversations.Add(conversation);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                // ─── 3. Send conversationId to client (first SSE event) ───
                var convId = conversation?.Id ?? 0;
                await Response.WriteAsync($"data: {{\"conversationId\":{convId}}}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);

                // ─── 4. Save user message to DB ───
                if (conversation != null)
                {
                    _db.ChatMessages.Add(new ChatMessage
                    {
                        ConversationId = conversation.Id,
                        Role = "user",
                        Content = dto.Message,
                        CreatedAt = DateTime.Now
                    });
                    await _db.SaveChangesAsync(cancellationToken);
                }

                // ─── 5. Build system prompt with OPMS context ───
                var systemPrompt = await _contextBuilder.BuildSystemPromptAsync(
                    User, _settings.SystemPrompt);

                var messages = new List<OllamaChatMessage>
                {
                    new() { Role = "system", Content = systemPrompt }
                };

                // ─── 6. Load history from DB (not from client) ───
                if (conversation != null)
                {
                    var dbMessages = await _db.ChatMessages
                        .Where(m => m.ConversationId == conversation.Id)
                        .OrderByDescending(m => m.CreatedAt)
                        .Take(10)
                        .OrderBy(m => m.CreatedAt)
                        .ToListAsync(cancellationToken);

                    foreach (var msg in dbMessages)
                    {
                        messages.Add(new OllamaChatMessage
                        {
                            Role = msg.Role,
                            Content = msg.Content
                        });
                    }
                }
                else
                {
                    // Fallback: use client-sent history if no DB conversation
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
                }

                // ─── 7. Stream from Ollama ───
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

                var fullResponse = new StringBuilder();

                await foreach (var token in _ollama.ChatStreamAsync(request, cancellationToken))
                {
                    fullResponse.Append(token);

                    // SSE format: data: {json}\n\n
                    var escaped = token.Replace("\\", "\\\\").Replace("\"", "\\\"")
                                       .Replace("\n", "\\n").Replace("\r", "\\r");
                    await Response.WriteAsync($"data: {{\"token\":\"{escaped}\"}}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }

                // ─── 8. Save assistant response to DB ───
                if (conversation != null && fullResponse.Length > 0)
                {
                    _db.ChatMessages.Add(new ChatMessage
                    {
                        ConversationId = conversation.Id,
                        Role = "assistant",
                        Content = fullResponse.ToString(),
                        CreatedAt = DateTime.Now
                    });

                    // Update conversation timestamp
                    conversation.LastMessageAt = DateTime.Now;
                    await _db.SaveChangesAsync(cancellationToken);
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
        //  GET /Chat/Conversations — List user's conversations
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> Conversations(CancellationToken cancellationToken)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Json(new List<object>());

            var conversations = await _db.ChatConversations
                .Where(c => c.UserId == userId.Value && !c.IsDeleted)
                .OrderByDescending(c => c.LastMessageAt)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.CreatedAt,
                    c.LastMessageAt,
                    MessageCount = c.Messages.Count
                })
                .Take(50)
                .ToListAsync(cancellationToken);

            return Json(conversations);
        }

        // ═══════════════════════════════════════════════════════════
        //  GET /Chat/Messages?conversationId=X — Load messages
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> Messages(int conversationId, CancellationToken cancellationToken)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            // Verify ownership
            var conversation = await _db.ChatConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId
                    && c.UserId == userId.Value
                    && !c.IsDeleted, cancellationToken);

            if (conversation == null)
                return NotFound();

            var messages = await _db.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    m.Role,
                    m.Content,
                    m.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return Json(messages);
        }

        // ═══════════════════════════════════════════════════════════
        //  POST /Chat/DeleteConversation — Soft delete
        // ═══════════════════════════════════════════════════════════

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteConversation([FromBody] DeleteConversationDto dto, CancellationToken cancellationToken)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            var conversation = await _db.ChatConversations
                .FirstOrDefaultAsync(c => c.Id == dto.ConversationId
                    && c.UserId == userId.Value
                    && !c.IsDeleted, cancellationToken);

            if (conversation == null)
                return NotFound();

            conversation.IsDeleted = true;
            await _db.SaveChangesAsync(cancellationToken);

            return Json(new { success = true });
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

        // ═══════════════════════════════════════════════════════════
        //  Helper: Get current user ID from Claims
        // ═══════════════════════════════════════════════════════════

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }
    }

    // DTO for delete endpoint
    public class DeleteConversationDto
    {
        public int ConversationId { get; set; }
    }
}
