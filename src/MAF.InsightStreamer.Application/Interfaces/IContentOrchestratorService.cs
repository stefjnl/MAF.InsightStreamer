using MAF.InsightStreamer.Domain.Models;

namespace MAF.InsightStreamer.Application.Interfaces
{
    public interface IContentOrchestratorService
    {
        Task<string> RunAsync(string input);
        
        /// <summary>
        /// Asks a question about a previously analyzed document using conversational context.
        /// </summary>
        /// <param name="question">The question to ask about the document</param>
        /// <param name="chunks">The document chunks to use as context</param>
        /// <param name="threadId">The thread identifier for maintaining conversation state</param>
        /// <param name="conversationHistory">The history of previous messages in the conversation</param>
        /// <returns>The answer to the question with relevant chunk references</returns>
        Task<string> AskQuestionAsync(
            string question,
            List<DocumentChunk> chunks,
            string threadId,
            List<ConversationMessage> conversationHistory);
    }
}