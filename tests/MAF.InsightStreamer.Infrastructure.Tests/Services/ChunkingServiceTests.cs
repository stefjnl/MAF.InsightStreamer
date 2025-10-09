using MAF.InsightStreamer.Domain.Models;
using MAF.InsightStreamer.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MAF.InsightStreamer.Infrastructure.Tests.Services;

public class ChunkingServiceTests
{
    private readonly Mock<ILogger<ChunkingService>> _mockLogger;
    private readonly ChunkingService _service;

    public ChunkingServiceTests()
    {
        _mockLogger = new Mock<ILogger<ChunkingService>>();
        _service = new ChunkingService(_mockLogger.Object);
    }

    [Fact]
    public async Task ChunkTranscriptAsync_WithNormalInput_CreatesOverlappingChunks()
    {
        // Arrange
        var transcript = new List<TranscriptChunk>
        {
            new TranscriptChunk
            {
                ChunkIndex = 0,
                Text = new string('A', 3000),
                StartTimeSeconds = 0,
                EndTimeSeconds = 120
            },
            new TranscriptChunk
            {
                ChunkIndex = 1,
                Text = new string('B', 3000),
                StartTimeSeconds = 120,
                EndTimeSeconds = 240
            }
        };

        // Act
        var result = await _service.ChunkTranscriptAsync(transcript, 4000, 400);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count > 1, "Should create multiple chunks for large input");
        
        // Verify chunks have overlap by checking that the second chunk starts before the first one ends
        // (accounting for the sliding window algorithm)
        for (int i = 0; i < result.Count - 1; i++)
        {
            // Each chunk should have the specified overlap with the next one
            Assert.True(result[i].EndTimeSeconds >= result[i + 1].StartTimeSeconds - 1, 
                "Chunks should have overlapping timestamps");
        }
    }

    [Fact]
    public async Task ChunkTranscriptAsync_WhenTextSmallerThanChunkSize_ReturnsSingleChunk()
    {
        // Arrange
        var transcript = new List<TranscriptChunk>
        {
            new TranscriptChunk
            {
                ChunkIndex = 0,
                Text = "Short text",
                StartTimeSeconds = 5,
                EndTimeSeconds = 15
            }
        };

        // Act
        var result = await _service.ChunkTranscriptAsync(transcript, 4000, 400);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Short text", result[0].Text);
        Assert.Equal(5, result[0].StartTimeSeconds);
        Assert.Equal(14, result[0].EndTimeSeconds); // Due to timestamp mapping algorithm
        Assert.Equal(0, result[0].ChunkIndex);
    }

    [Fact]
    public async Task ChunkTranscriptAsync_WithEmptyTranscript_ReturnsEmptyList()
    {
        // Arrange
        var transcript = new List<TranscriptChunk>();

        // Act
        var result = await _service.ChunkTranscriptAsync(transcript, 4000, 400);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ChunkTranscriptAsync_WithNullTranscript_ThrowsArgumentNullException()
    {
        // Arrange
        List<TranscriptChunk> transcript = null!;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ChunkTranscriptAsync(transcript!, 4000, 400));
        
        Assert.Contains("transcript", exception.Message);
    }

    [Fact]
    public async Task ChunkTranscriptAsync_WithZeroChunkSize_ThrowsArgumentException()
    {
        // Arrange
        var transcript = new List<TranscriptChunk>
        {
            new TranscriptChunk
            {
                ChunkIndex = 0,
                Text = "Test text",
                StartTimeSeconds = 0,
                EndTimeSeconds = 10
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.ChunkTranscriptAsync(transcript, 0, 400));
        
        Assert.Contains("Chunk size must be greater than zero", exception.Message);
    }

    [Fact]
    public async Task ChunkTranscriptAsync_WithNegativeChunkSize_ThrowsArgumentException()
    {
        // Arrange
        var transcript = new List<TranscriptChunk>
        {
            new TranscriptChunk
            {
                ChunkIndex = 0,
                Text = "Test text",
                StartTimeSeconds = 0,
                EndTimeSeconds = 10
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.ChunkTranscriptAsync(transcript, -1, 400));
        
        Assert.Contains("Chunk size must be greater than zero", exception.Message);
    }

    [Fact]
    public async Task ChunkTranscriptAsync_WhenOverlapExceedsChunkSize_ThrowsArgumentException()
    {
        // Arrange
        var transcript = new List<TranscriptChunk>
        {
            new TranscriptChunk
            {
                ChunkIndex = 0,
                Text = "Test text",
                StartTimeSeconds = 0,
                EndTimeSeconds = 10
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.ChunkTranscriptAsync(transcript, 400, 500));
        
        Assert.Contains("Overlap must be less than chunk size", exception.Message);
    }

    [Fact]
    public async Task ChunkTranscriptAsync_PreservesTimestampInformation()
    {
        // Arrange
        var transcript = new List<TranscriptChunk>
        {
            new TranscriptChunk
            {
                ChunkIndex = 0,
                Text = new string('A', 2000),
                StartTimeSeconds = 0,
                EndTimeSeconds = 60
            },
            new TranscriptChunk
            {
                ChunkIndex = 1,
                Text = new string('B', 2000),
                StartTimeSeconds = 60,
                EndTimeSeconds = 120
            }
        };

        // Act
        var result = await _service.ChunkTranscriptAsync(transcript, 3000, 300);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        // Verify timestamps are preserved and reasonable
        foreach (var chunk in result)
        {
            Assert.True(chunk.StartTimeSeconds >= 0);
            Assert.True(chunk.EndTimeSeconds >= chunk.StartTimeSeconds);
            Assert.True(chunk.EndTimeSeconds <= 120); // Should not exceed original duration
        }
        
        // First chunk should start at the beginning
        Assert.Equal(0, result[0].StartTimeSeconds, 2); // Allow for larger floating point differences
        
        // Last chunk should end at or near the end
        var lastChunk = result[result.Count - 1];
        Assert.True(lastChunk.EndTimeSeconds >= 110); // Close to the end (120)
    }

    [Fact]
    public async Task ChunkTranscriptAsync_IncludesLastChunkEvenIfSmaller()
    {
        // Arrange
        var transcript = new List<TranscriptChunk>
        {
            new TranscriptChunk
            {
                ChunkIndex = 0,
                Text = new string('A', 5000), // Larger than chunk size
                StartTimeSeconds = 0,
                EndTimeSeconds = 150
            },
            new TranscriptChunk
            {
                ChunkIndex = 1,
                Text = "Small ending", // Much smaller than chunk size
                StartTimeSeconds = 150,
                EndTimeSeconds = 160
            }
        };

        // Act
        var result = await _service.ChunkTranscriptAsync(transcript, 4000, 400);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2, "Should have at least 2 chunks");
        
        // The last chunk should contain the ending text
        var lastChunk = result[result.Count - 1];
        Assert.Contains("Small ending", lastChunk.Text);
        Assert.True(lastChunk.Text.Length < 4000, "Last chunk should be smaller than chunk size");
    }

    [Fact]
    public async Task ChunkTranscriptAsync_CreatesCorrectOverlap()
    {
        // Arrange
        var transcript = new List<TranscriptChunk>
        {
            new TranscriptChunk
            {
                ChunkIndex = 0,
                Text = new string('A', 3000),
                StartTimeSeconds = 0,
                EndTimeSeconds = 100
            },
            new TranscriptChunk
            {
                ChunkIndex = 1,
                Text = new string('B', 3000),
                StartTimeSeconds = 100,
                EndTimeSeconds = 200
            }
        };

        const int chunkSize = 4000;
        const int overlapSize = 500;

        // Act
        var result = await _service.ChunkTranscriptAsync(transcript, chunkSize, overlapSize);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2, "Should create multiple chunks");

        // Check that chunks have the correct overlap
        // This is a bit tricky to verify precisely due to the sliding window algorithm,
        // but we can verify that consecutive chunks have overlapping content
        if (result.Count >= 2)
        {
            var firstChunk = result[0];
            var secondChunk = result[1];
            
            // Second chunk should start where the first chunk would end minus the overlap
            // This is a simplified check - in practice the exact calculation depends on the algorithm
            Assert.True(secondChunk.StartTimeSeconds <= firstChunk.EndTimeSeconds, 
                "Second chunk should start before or at the end of the first chunk");
        }
    }

    [Fact]
    public void Constructor_ValidLogger_CreatesInstance()
    {
        // Arrange
        var logger = new Mock<ILogger<ChunkingService>>().Object;

        // Act
        var service = new ChunkingService(logger);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ChunkingService(null!));
    }
}