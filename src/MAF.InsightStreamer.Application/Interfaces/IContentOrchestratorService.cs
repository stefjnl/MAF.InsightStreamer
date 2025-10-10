namespace MAF.InsightStreamer.Application.Interfaces
{
    public interface IContentOrchestratorService
    {
        Task<string> RunAsync(string input);
    }
}