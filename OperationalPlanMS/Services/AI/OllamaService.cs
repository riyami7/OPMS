using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OperationalPlanMS.Services.AI.Models;

namespace OperationalPlanMS.Services.AI
{
    public class OllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly OllamaSettings _settings;
        private readonly ILogger<OllamaService> _logger;

        public OllamaService(
            HttpClient httpClient,
            IOptions<OllamaSettings> settings,
            ILogger<OllamaService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        }

        // ═══════════════════════════════════════════════════════════
        //  Chat (complete response)
        // ═══════════════════════════════════════════════════════════

        public async Task<OllamaChatResponse> ChatAsync(
            OllamaChatRequest request,
            CancellationToken cancellationToken = default)
        {
            request.Stream = false;
            request.Model ??= _settings.DefaultModel;
            request.Options ??= new OllamaOptions { Temperature = _settings.Temperature };

            // Handle thinking mode
            if (!_settings.EnableThinking && request.Think == null)
                request.Think = false;

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending chat request to Ollama model: {Model}", request.Model);

            var response = await _httpClient.PostAsync("/api/chat", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<OllamaChatResponse>(responseJson);
        }

        // ═══════════════════════════════════════════════════════════
        //  Chat Stream (token by token via SSE)
        // ═══════════════════════════════════════════════════════════

        public async IAsyncEnumerable<string> ChatStreamAsync(
            OllamaChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            request.Stream = true;
            request.Model ??= _settings.DefaultModel;
            request.Options ??= new OllamaOptions { Temperature = _settings.Temperature };

            // Handle thinking mode
            if (!_settings.EnableThinking && request.Think == null)
                request.Think = false;

            var json = JsonSerializer.Serialize(request);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            _logger.LogInformation("Starting streaming chat with Ollama model: {Model}", request.Model);

            var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            bool insideThinkBlock = false;

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                OllamaStreamChunk chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Ollama stream chunk");
                    continue;
                }

                if (chunk?.Done == true) break;

                var token = chunk?.Message?.Content;
                if (string.IsNullOrEmpty(token)) continue;

                // Filter out <think>...</think> blocks from qwen3
                if (!_settings.EnableThinking)
                {
                    if (token.Contains("<think>"))
                    {
                        insideThinkBlock = true;
                        continue;
                    }
                    if (insideThinkBlock)
                    {
                        if (token.Contains("</think>"))
                        {
                            insideThinkBlock = false;
                        }
                        continue;
                    }
                }

                yield return token;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  List Models
        // ═══════════════════════════════════════════════════════════

        public async Task<List<OllamaModelInfo>> ListModelsAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<OllamaTagsResponse>(json);

                return result?.Models ?? new List<OllamaModelInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list Ollama models");
                return new List<OllamaModelInfo>();
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Health Check
        // ═══════════════════════════════════════════════════════════

        public async Task<bool> IsAvailableAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
