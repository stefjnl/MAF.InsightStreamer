using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;
namespace MAF.InsightStreamer.Infrastructure.Orchestration;

public class VideoOrchestratorService
{
    private readonly AIAgent _orchestrator;

    public VideoOrchestratorService(string apiKey, string model, string endpoint)
    {
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
                Name = "VideoAnalysisOrchestrator",
                Instructions = "Coordinate YouTube video extraction and summarization. Use available tools to accomplish tasks.",
                ChatOptions = new ChatOptions
                {
                    Tools = [
                        // Tools will be added here
                        // AIFunctionFactory.Create(ExtractYouTubeVideo),
                        // AIFunctionFactory.Create(SummarizeTranscript)
                    ]
                }
            }
        );
    }

    public async Task<string> RunAsync(string input)
    {
        AgentRunResponse response = await _orchestrator.RunAsync(input);
        return response.Text;
    }
}