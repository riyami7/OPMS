using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OperationalPlanMS.Services.AI.Models
{
    // ═══════════════════════════════════════════════════════════
    //  Settings (appsettings.json)
    // ═══════════════════════════════════════════════════════════

    public class OllamaSettings
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string DefaultModel { get; set; } = "qwen3:8b";
        public string SystemPrompt { get; set; } = "أنت مساعد ذكي لنظام إدارة الخطط التشغيلية (OPMS). أجب باللغة العربية بشكل مختصر ومفيد.";
        public int TimeoutSeconds { get; set; } = 120;
        public double Temperature { get; set; } = 0.7;
        public bool EnableThinking { get; set; } = false;
    }

    // ═══════════════════════════════════════════════════════════
    //  Chat Request (to Ollama API)
    // ═══════════════════════════════════════════════════════════

    public class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("messages")]
        public List<OllamaChatMessage> Messages { get; set; } = new();

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = true;

        [JsonPropertyName("options")]
        public OllamaOptions Options { get; set; }

        /// <summary>
        /// Optional: override thinking mode per request
        /// </summary>
        [JsonPropertyName("think")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Think { get; set; }
    }

    public class OllamaChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } // "system", "user", "assistant"

        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    public class OllamaOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.7;

        [JsonPropertyName("num_predict")]
        public int MaxTokens { get; set; } = 2048;
    }

    // ═══════════════════════════════════════════════════════════
    //  Chat Response (from Ollama API)
    // ═══════════════════════════════════════════════════════════

    public class OllamaChatResponse
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("message")]
        public OllamaChatMessage Message { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("total_duration")]
        public long? TotalDuration { get; set; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; }
    }

    /// <summary>
    /// Streamed chunk from Ollama
    /// </summary>
    public class OllamaStreamChunk
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("message")]
        public OllamaChatMessage Message { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    //  Model Info (from /api/tags)
    // ═══════════════════════════════════════════════════════════

    public class OllamaModelInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("modified_at")]
        public DateTime ModifiedAt { get; set; }

        public string SizeFormatted => Size > 1_000_000_000
            ? $"{Size / 1_000_000_000.0:F1} GB"
            : $"{Size / 1_000_000.0:F0} MB";
    }

    public class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo> Models { get; set; } = new();
    }

    // ═══════════════════════════════════════════════════════════
    //  DTOs for the Chat API (Browser <-> OPMS)
    // ═══════════════════════════════════════════════════════════

    public class ChatRequestDto
    {
        public string Message { get; set; }
        public string Model { get; set; }
        public int? ConversationId { get; set; }
        public List<ChatHistoryItem> History { get; set; } = new();
    }

    public class ChatHistoryItem
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }
}
