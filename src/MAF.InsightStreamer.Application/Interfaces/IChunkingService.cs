namespace MAF.InsightStreamer.Application.Interfaces;

using MAF.InsightStreamer.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Defines the contract for chunking services.
/// Provides methods to split text into overlapping chunks for LLM processing.
/// </summary>
public interface IChunkingService
{
    /// <summary>
    /// Splits transcript into overlapping chunks for LLM processing.
    /// </summary>
    /// <param name="transcript">Original transcript chunks with timestamps</param>
    /// <param name="chunkSize">Target characters per chunk (default: 4000 ≈ 1000 tokens)</param>
    /// <param name="overlapSize">Overlap between chunks (default: 400 ≈ 100 tokens)</param>
    /// <returns>List of chunked transcript segments with preserved timestamps</returns>
    Task<List<TranscriptChunk>> ChunkTranscriptAsync(
        List<TranscriptChunk> transcript,
        int chunkSize = 4000,
        int overlapSize = 400);

    /// <summary>
    /// Splits document text into overlapping chunks for Q&A processing.
    /// </summary>
    /// <param name="documentText">The full text content of the document</param>
    /// <param name="chunkSize">Target characters per chunk (default: 4000 ≈ 1000 tokens)</param>
    /// <param name="overlapSize">Overlap between chunks (default: 400 ≈ 100 tokens)</param>
    /// <returns>List of document chunks with position information</returns>
    Task<List<DocumentChunk>> ChunkDocumentAsync(
        string documentText,
        int chunkSize = 4000,
        int overlapSize = 400);
}