using MAF.InsightStreamer.Domain.Models;

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
        /// <returns>A unique thread identifier</returns>
        Task<string> CreateThreadForDocumentAsync(Guid sessionId);

        /// <summary>
        /// Retrieves an existing thread by its identifier.
        /// </summary>
        /// <param name="threadId">The thread identifier</param>
        /// <returns>ConversationThread instance if found, null otherwise</returns>
        Task<ConversationThread?> GetThreadAsync(string threadId);

        /// <summary>
        /// Removes a thread from storage.
        /// </summary>
        /// <param name="threadId">The thread identifier to remove</param>
        Task RemoveThreadAsync(string threadId);

        /// <summary>
        /// Gets the underlying AIAgent for a thread (for internal use by orchestrator).
        /// </summary>
        /// <param name="threadId">The thread identifier</param>
        /// <returns>The AIAgent instance if found, null otherwise</returns>
        Task<object?> GetAgentAsync(string threadId);
    }
}