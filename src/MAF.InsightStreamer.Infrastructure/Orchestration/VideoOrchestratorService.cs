using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;
using System.Linq;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Infrastructure.Providers;

namespace MAF.InsightStreamer.Infrastructure.Orchestration;

public class VideoOrchestratorService : IVideoOrchestratorService
{
    private readonly AIAgent _orchestrator;
    private readonly IYouTubeService _youtubeService;
    private readonly IChunkingService _chunkingService;

    public VideoOrchestratorService(
        IOptions<ProviderSettings> settings,
        IYouTubeService youtubeService,
        IChunkingService chunkingService)
    {
        _youtubeService = youtubeService ?? throw new ArgumentNullException(nameof(youtubeService));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));

        var config = settings?.Value ?? throw new ArgumentNullException(nameof(settings));

        ChatClient chatClient = new(
            model: config.Model,
            credential: new ApiKeyCredential(config.ApiKey),
            options: new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(config.Endpoint)
            }
        );

        IChatClient client = chatClient.AsIChatClient();

        _orchestrator = new ChatClientAgent(
            client,
            new ChatClientAgentOptions
            {
                Name = "VideoOrchestratorAgent",
                Instructions = "You are a helpful assistant that can extract and analyze YouTube videos.",
                ChatOptions = new ChatOptions
                {
                    Tools = [
                        AIFunctionFactory.Create(ExtractYouTubeVideo),
                        AIFunctionFactory.Create(ChunkTranscriptForAnalysis)
                    ]
                }
            }
        );
    }

    [Description("Extract transcript and metadata from a YouTube video URL")]
    public async Task<string> ExtractYouTubeVideo(
        [Description("The YouTube video URL to extract")] string videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            throw new ArgumentException("Video URL cannot be null or empty", nameof(videoUrl));
        }

        try
        {
            var metadata = await _youtubeService.GetVideoMetadataAsync(videoUrl);
            var transcript = await _youtubeService.GetTranscriptAsync(videoUrl);

            // Store in cache (future implementation)
            // Return structured summary for LLM
            return $"Extracted video: {metadata.Title} by {metadata.Author}. " +
                   $"Duration: {metadata.Duration}. Transcript has {transcript.Count} segments.";
        }
        catch (Exception ex)
        {
            return $"Error extracting video: {ex.Message}";
        }
    }

    [Description("Extract and chunk a YouTube video transcript into overlapping segments for analysis")]
    public async Task<string> ChunkTranscriptForAnalysis(
        [Description("The YouTube video URL to process")] string videoUrl,
        [Description("Characters per chunk (default: 4000 ≈ 1000 tokens)")] int chunkSize = 4000,
        [Description("Overlap between chunks in characters (default: 400 ≈ 100 tokens)")] int overlapSize = 400)
    {
        try
        {
            // Step 1: Extract video metadata and transcript
            var metadata = await _youtubeService.GetVideoMetadataAsync(videoUrl);
            var transcript = await _youtubeService.GetTranscriptAsync(videoUrl);

            // Step 2: Chunk the transcript
            var chunks = await _chunkingService.ChunkTranscriptAsync(transcript, chunkSize, overlapSize);

            // Step 3: Format response for LLM
            var totalChars = chunks.Sum(c => c.Text.Length);
            return $"Successfully chunked video '{metadata.Title}' (Duration: {metadata.Duration})\n" +
                   $"Generated {chunks.Count} chunks from {transcript.Count} original segments\n" +
                   $"Total characters: {totalChars}\n" +
                   $"Chunk size: {chunkSize} chars, Overlap: {overlapSize} chars\n" +
                   $"First chunk preview: {chunks[0].Text.Substring(0, Math.Min(100, chunks[0].Text.Length))}...";
        }
        catch (Exception ex)
        {
            return $"Error chunking transcript: {ex.Message}";
        }
    }

    public async Task<string> RunAsync(string input)
    {
        AgentRunResponse response = await _orchestrator.RunAsync(input);
        return response.Text;
    }
}