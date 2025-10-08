using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;
using MAF.InsightStreamer.Application.Interfaces;

namespace MAF.InsightStreamer.Infrastructure.Orchestration;

public class VideoOrchestratorService : IVideoOrchestratorService
{
    private readonly AIAgent _orchestrator;
    private readonly IYouTubeService _youtubeService;

    public VideoOrchestratorService(string apiKey, string model, string endpoint, IYouTubeService youtubeService)
    {
        _youtubeService = youtubeService;
        
        ChatClient chatClient = new(
            model: model,
            credential: new ApiKeyCredential(apiKey),
            options: new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint)
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