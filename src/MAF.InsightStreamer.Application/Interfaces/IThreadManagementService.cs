using MAF.InsightStreamer.Domain.Models;
using System.Threading;
using System.Threading.Tasks;

namespace MAF.InsightStreamer.Application.Interfaces
{
    /// <summary>
    /// Manages the lifecycle of conversation threads for document Q&A interactions.
    /// Abstracts thread storage and retrieval from the orchestrator.
    /// </summary>
    public interface IThreadManagementService
    {
        /// <summary>
        /// Creates a new thread for a document session.
        /// </summary>
        /// <param name="sessionId">The unique identifier for the document session</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>A unique thread identifier</returns>
        Task<string> CreateThreadForDocumentAsync(Guid sessionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an existing thread by its identifier.
        /// </summary>
        /// <param name="threadId">The thread identifier</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>ConversationThread instance if found, null otherwise</returns>
        Task<ConversationThread?> GetThreadAsync(string threadId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a thread from storage.
        /// </summary>
        /// <param name="threadId">The thread identifier to remove</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        Task RemoveThreadAsync(string threadId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the underlying AIAgent for a thread (for internal use by orchestrator).
        /// </summary>
        /// <param name="threadId">The thread identifier</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>The AIAgent instance if found, null otherwise</returns>
        Task<object?> GetAgentAsync(string threadId, CancellationToken cancellationToken = default);
    }
}