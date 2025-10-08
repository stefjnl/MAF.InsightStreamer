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
    private readonly AIAgent _orchestrator;
    private readonly IYouTubeService _youtubeService;

    public VideoOrchestratorService(
        IOptions<ProviderSettings> settings,
        IYouTubeService youtubeService)
    {
        _youtubeService = youtubeService ?? throw new ArgumentNullException(nameof(youtubeService));

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
                        AIFunctionFactory.Create(ExtractYouTubeVideo)
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

    public async Task<string> RunAsync(string input)
    {
        AgentRunResponse response = await _orchestrator.RunAsync(input);
        return response.Text;
    }
}