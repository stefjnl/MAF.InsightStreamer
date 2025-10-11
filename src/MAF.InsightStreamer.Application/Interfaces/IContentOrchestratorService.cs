using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Domain.Models;
using MAF.InsightStreamer.Application.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace MAF.InsightStreamer.Application.Interfaces
{
    public interface IContentOrchestratorService
    {
        Task<string> RunAsync(string input, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Asks a question about a previously analyzed document using conversational context.
        /// </summary>
        /// <param name="question">The question to ask about the document</param>
        /// <param name="chunks">The document chunks to use as context</param>
        /// <param name="threadId">The thread identifier for maintaining conversation state</param>
        /// <param name="conversationHistory">The history of previous messages in the conversation</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>The answer to the question with relevant chunk references</returns>
        Task<string> AskQuestionAsync(
            string question,
            List<DocumentChunk> chunks,
            string threadId,
            List<ConversationMessage> conversationHistory,
            CancellationToken cancellationToken = default);
        /// <summary>
        /// Switches the current provider configuration for the orchestrator.
        /// </summary>
        /// <param name="provider">The new provider</param>
        /// <param name="model">The new model</param>
        /// <param name="endpoint">The new endpoint</param>
        /// <param name="apiKey">The API key (if required by the provider)</param>
        void SwitchProvider(ModelProvider provider, string model, string endpoint, string? apiKey);
        
        /// <summary>
        /// Gets the current provider configuration
        /// </summary>
        /// <returns>Current provider configuration</returns>
        ProviderConfiguration GetCurrentProviderConfiguration();
    }
}