using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Infrastructure.Providers;

namespace MAF.InsightStreamer.Infrastructure.Orchestration;

public class VideoOrchestratorService : IVideoOrchestratorService
{
    private readonly IYouTubeService _youtubeService;
    private readonly IChunkingService _chunkingService;
    private readonly AIAgent _orchestrator;

    public VideoOrchestratorService(
        IOptions<ProviderSettings> settings,
        IYouTubeService youtubeService,
        IChunkingService chunkingService)
    {
        _youtubeService = youtubeService;
        _chunkingService = chunkingService;

        var config = settings.Value;

        // Create ChatClient
        ChatClient chatClient = new(
            model: config.Model,
            credential: new ApiKeyCredential(config.ApiKey),
            options: new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(config.Endpoint)
            }
        );

        // Convert to IChatClient
        IChatClient client = chatClient.AsIChatClient();

        // Create orchestrator with ALL tools registered
        _orchestrator = new ChatClientAgent(
            client,
            new ChatClientAgentOptions
            {
                Name = "VideoOrchestratorAgent",
                Instructions = "You coordinate YouTube video analysis workflows. Use available tools to extract, chunk, and summarize video content.",
                ChatOptions = new ChatOptions
                {
                    Tools = [
                        AIFunctionFactory.Create(ExtractYouTubeVideo),
                        AIFunctionFactory.Create(ChunkTranscriptForAnalysis),
                        AIFunctionFactory.Create(SummarizeVideo)
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
            var metadata = await _youtubeService.GetVideoMetadataAsync(videoUrl);
            var transcript = await _youtubeService.GetTranscriptAsync(videoUrl);
            var chunks = await _chunkingService.ChunkTranscriptAsync(transcript, chunkSize, overlapSize);

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

    [Description("Generate a concise 3-5 bullet point summary of a YouTube video's content")]
    public async Task<string> SummarizeVideo(
        [Description("The YouTube video URL to summarize")] string videoUrl,
        [Description("Maximum number of chunks to process (default: 10)")] int maxChunks = 10)
    {
        try
        {
            var metadata = await _youtubeService.GetVideoMetadataAsync(videoUrl);
            var transcript = await _youtubeService.GetTranscriptAsync(videoUrl);
            var chunks = await _chunkingService.ChunkTranscriptAsync(transcript, 4000, 400);

            var chunksToProcess = chunks.Take(maxChunks).ToList();
            var combinedText = string.Join("\n\n", chunksToProcess.Select(c => c.Text));

            var prompt = $@"You are a video content summarization expert. Summarize this YouTube video transcript in exactly 3-5 concise bullet points focusing on main topics and key takeaways.

Video Title: {metadata.Title}
Video Duration: {metadata.Duration}
Channel: {metadata.Author}

Transcript (first {chunksToProcess.Count} segments):
{combinedText}

Provide the summary as bullet points starting with '•' or '-'.";

            // Use orchestrator's RunAsync (it will use the LLM internally)
            var summary = await _orchestrator.RunAsync(prompt);

            return $"Summary of '{metadata.Title}' ({metadata.Duration}):\n\n{summary}\n\n" +
                   $"Note: Summary based on first {chunksToProcess.Count} of {chunks.Count} total chunks.";
        }
        catch (Exception ex)
        {
            return $"Error generating summary for video: {ex.Message}";
        }
    }

    public async Task<string> RunAsync(string input)
    {
        AgentRunResponse response = await _orchestrator.RunAsync(input);
        return response.Text;
    }
}