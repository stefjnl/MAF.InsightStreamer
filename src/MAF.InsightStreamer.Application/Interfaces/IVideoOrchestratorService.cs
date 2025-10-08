namespace MAF.InsightStreamer.Application.Interfaces
{
    public interface IVideoOrchestratorService
    {
        Task<string> RunAsync(string input);
    }
}
