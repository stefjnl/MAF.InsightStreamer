using System.Threading;
using System.Threading.Tasks;

namespace MAF.InsightStreamer.Application.Interfaces
{
    /// <summary>
    /// Handles migration and preservation of conversation threads when switching model providers.
    /// Starts with a simple reset strategy (Option B) and can evolve to serialization (Option A).
    /// </summary>
    public interface IThreadMigrationService
    {
        /// <summary>
        /// Performs a safe reset of all conversation threads when switching models.
        /// Returns a user-visible warning message describing the reset.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<string> ResetOnModelSwitchAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Placeholder for future preservation of a specific thread's state before switching providers.
        /// </summary>
        /// <param name="threadId">The thread identifier to preserve</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task PreserveThreadStateAsync(string threadId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Placeholder for future restore of a thread's state after switching providers.
        /// </summary>
        /// <param name="threadId">The thread identifier to restore</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RestoreThreadStateAsync(string threadId, CancellationToken cancellationToken = default);
    }
}