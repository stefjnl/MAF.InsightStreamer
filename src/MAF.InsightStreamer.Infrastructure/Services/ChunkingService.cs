namespace MAF.InsightStreamer.Infrastructure.Services;

using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implementation of chunking service.
/// Provides methods to split text into overlapping chunks for LLM processing.
/// </summary>
public class ChunkingService : IChunkingService
{
    private readonly ILogger<ChunkingService> _logger;

    /// <summary>
    /// Initializes a new instance of the ChunkingService class.
    /// </summary>
    /// <param name="logger">The logger instance for recording service operations.</param>
    public ChunkingService(ILogger<ChunkingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Splits transcript into overlapping chunks for LLM processing using a sliding window algorithm.
    /// </summary>
    /// <param name="transcript">Original transcript chunks with timestamps</param>
    /// <param name="chunkSize">Target characters per chunk (default: 4000 ≈ 1000 tokens)</param>
    /// <param name="overlapSize">Overlap between chunks (default: 400 ≈ 100 tokens)</param>
    /// <returns>List of chunked transcript segments with preserved timestamps</returns>
    /// <exception cref="ArgumentNullException">Thrown when the transcript is null.</exception>
    /// <exception cref="ArgumentException">Thrown when chunkSize is less than or equal to zero, or overlapSize is negative, or overlapSize is greater than or equal to chunkSize.</exception>
    public Task<List<TranscriptChunk>> ChunkTranscriptAsync(
        List<TranscriptChunk> transcript,
        int chunkSize = 4000,
        int overlapSize = 400,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (transcript == null)
        {
            _logger.LogError("Transcript cannot be null");
            throw new ArgumentNullException(nameof(transcript));
        }

        if (chunkSize <= 0)
        {
            _logger.LogError("Chunk size must be greater than zero. Provided value: {ChunkSize}", chunkSize);
            throw new ArgumentException("Chunk size must be greater than zero", nameof(chunkSize));
        }

        if (overlapSize < 0)
        {
            _logger.LogError("Overlap size cannot be negative. Provided value: {OverlapSize}", overlapSize);
            throw new ArgumentException("Overlap size cannot be negative", nameof(overlapSize));
        }

        if (overlapSize >= chunkSize)
        {
            _logger.LogError("Overlap size must be less than chunk size. Chunk size: {ChunkSize}, Overlap size: {OverlapSize}", chunkSize, overlapSize);
            throw new ArgumentException("Overlap must be less than chunk size", nameof(overlapSize));
        }

        // Handle empty transcript
        if (transcript.Count == 0)
        {
            _logger.LogWarning("Empty transcript provided, returning empty chunk list");
            return Task.FromResult(new List<TranscriptChunk>());
        }

        _logger.LogInformation("Chunking transcript with {TranscriptCount} segments, chunk size: {ChunkSize}, overlap size: {OverlapSize}", transcript.Count, chunkSize, overlapSize);

        // Create a mapping of character positions to timestamps
        var fullText = new StringBuilder();
        var positionToTimestamp = new List<double>();
        
        for (int i = 0; i < transcript.Count; i++)
        {
            var chunk = transcript[i];
            var textLength = chunk.Text.Length;
            
            // Add the text to our full text
            fullText.Append(chunk.Text);
            
            // Map each character position to its timestamp
            if (textLength > 0)
            {
                var startTime = chunk.StartTimeSeconds;
                var endTime = chunk.EndTimeSeconds;
                var durationPerCharacter = (endTime - startTime) / textLength;
                
                for (int j = 0; j < textLength; j++)
                {
                    var timestamp = startTime + (j * durationPerCharacter);
                    positionToTimestamp.Add(timestamp);
                }
            }
            
            // Add space between segments (except for the last one)
            if (i < transcript.Count - 1)
            {
                fullText.Append(" ");
                // For the space character, use the end time of the current chunk
                positionToTimestamp.Add(chunk.EndTimeSeconds);
            }
        }

        var text = fullText.ToString();
        _logger.LogDebug("Full concatenated transcript text length: {TextLength}", text.Length);

        // Handle case where text is smaller than chunk size
        if (text.Length <= chunkSize)
        {
            var result = new List<TranscriptChunk>
            {
                new TranscriptChunk
                {
                    ChunkIndex = 0,
                    Text = text,
                    StartTimeSeconds = positionToTimestamp.FirstOrDefault(),
                    EndTimeSeconds = positionToTimestamp.LastOrDefault()
                }
            };
            
            _logger.LogInformation("Text length ({TextLength}) is smaller than chunk size ({ChunkSize}), returning single chunk", text.Length, chunkSize);
            return Task.FromResult(result);
        }

        // Apply sliding window algorithm
        var chunks = new List<TranscriptChunk>();
        int position = 0;
        int chunkIndex = 0;

        while (position < text.Length)
        {
            // Determine the end position for this chunk
            var endPosition = Math.Min(position + chunkSize, text.Length);
            var chunkText = text.Substring(position, endPosition - position);

            // Get timestamps for start and end positions
            var startTime = position < positionToTimestamp.Count ? positionToTimestamp[position] : positionToTimestamp.LastOrDefault();
            var endTime = (endPosition - 1) < positionToTimestamp.Count ? positionToTimestamp[endPosition - 1] : positionToTimestamp.LastOrDefault();

            var chunk = new TranscriptChunk
            {
                ChunkIndex = chunkIndex++,
                Text = chunkText,
                StartTimeSeconds = startTime,
                EndTimeSeconds = endTime
            };

            chunks.Add(chunk);

            _logger.LogDebug("Created chunk {ChunkIndex} with length {ChunkLength} characters, start time: {StartTime}, end time: {EndTime}", 
                chunk.ChunkIndex, chunk.Text.Length, chunk.StartTimeSeconds, chunk.EndTimeSeconds);

            // Advance position by (chunkSize - overlapSize) for overlap
            position += (chunkSize - overlapSize);

            // If we've reached the end, break
            if (position >= text.Length)
                break;
        }

        _logger.LogInformation("Successfully chunked transcript into {ChunkCount} chunks", chunks.Count);
        return Task.FromResult(chunks);
    }

    /// <summary>
    /// Splits document text into overlapping chunks for Q&A processing using a sliding window algorithm.
    /// </summary>
    /// <param name="documentText">The full text content of the document</param>
    /// <param name="chunkSize">Target characters per chunk (default: 4000 ≈ 1000 tokens)</param>
    /// <param name="overlapSize">Overlap between chunks (default: 400 ≈ 100 tokens)</param>
    /// <returns>List of document chunks with position information</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documentText is null.</exception>
    /// <exception cref="ArgumentException">Thrown when chunkSize is less than or equal to zero, or overlapSize is negative, or overlapSize is greater than or equal to chunkSize.</exception>
    public Task<List<DocumentChunk>> ChunkDocumentAsync(
        string documentText,
        int chunkSize = 4000,
        int overlapSize = 400,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (documentText == null)
        {
            _logger.LogError("Document text cannot be null");
            throw new ArgumentNullException(nameof(documentText));
        }

        if (chunkSize <= 0)
        {
            _logger.LogError("Chunk size must be greater than zero. Provided value: {ChunkSize}", chunkSize);
            throw new ArgumentException("Chunk size must be greater than zero", nameof(chunkSize));
        }

        if (overlapSize < 0)
        {
            _logger.LogError("Overlap size cannot be negative. Provided value: {OverlapSize}", overlapSize);
            throw new ArgumentException("Overlap size cannot be negative", nameof(overlapSize));
        }

        if (overlapSize >= chunkSize)
        {
            _logger.LogError("Overlap size must be less than chunk size. Chunk size: {ChunkSize}, Overlap size: {OverlapSize}", chunkSize, overlapSize);
            throw new ArgumentException("Overlap must be less than chunk size", nameof(overlapSize));
        }

        // Handle empty document
        if (string.IsNullOrWhiteSpace(documentText))
        {
            _logger.LogWarning("Empty document text provided, returning empty chunk list");
            return Task.FromResult(new List<DocumentChunk>());
        }

        _logger.LogInformation("Chunking document with text length: {TextLength}, chunk size: {ChunkSize}, overlap size: {OverlapSize}", documentText.Length, chunkSize, overlapSize);

        // Handle case where text is smaller than chunk size
        if (documentText.Length <= chunkSize)
        {
            var result = new List<DocumentChunk>
            {
                new DocumentChunk
                {
                    ChunkIndex = 0,
                    Content = documentText,
                    StartPosition = 0,
                    EndPosition = documentText.Length
                }
            };
            
            _logger.LogInformation("Text length ({TextLength}) is smaller than chunk size ({ChunkSize}), returning single chunk", documentText.Length, chunkSize);
            return Task.FromResult(result);
        }

        // Apply sliding window algorithm
        var chunks = new List<DocumentChunk>();
        int position = 0;
        int chunkIndex = 0;

        while (position < documentText.Length)
        {
            // Determine the end position for this chunk
            var endPosition = Math.Min(position + chunkSize, documentText.Length);
            var chunkText = documentText.Substring(position, endPosition - position);

            var chunk = new DocumentChunk
            {
                ChunkIndex = chunkIndex++,
                Content = chunkText,
                StartPosition = position,
                EndPosition = endPosition
            };

            chunks.Add(chunk);

            _logger.LogDebug("Created chunk {ChunkIndex} with length {ChunkLength} characters, start position: {StartPosition}, end position: {EndPosition}",
                chunk.ChunkIndex, chunk.Content.Length, chunk.StartPosition, chunk.EndPosition);

            // Advance position by (chunkSize - overlapSize) for overlap
            position += (chunkSize - overlapSize);

            // If we've reached the end, break
            if (position >= documentText.Length)
                break;
        }

        _logger.LogInformation("Successfully chunked document into {ChunkCount} chunks", chunks.Count);
        return Task.FromResult(chunks);
    }
}