using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OperationalPlanMS.Services.AI.Models;

namespace OperationalPlanMS.Services.AI
{
    public interface IOllamaService
    {
        /// <summary>
        /// Send a chat message and get a complete response
        /// </summary>
        Task<OllamaChatResponse> ChatAsync(OllamaChatRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a chat message and stream the response token by token
        /// </summary>
        IAsyncEnumerable<string> ChatStreamAsync(OllamaChatRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// List available models on the Ollama server
        /// </summary>
        Task<List<OllamaModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if Ollama server is reachable
        /// </summary>
        Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    }
}
