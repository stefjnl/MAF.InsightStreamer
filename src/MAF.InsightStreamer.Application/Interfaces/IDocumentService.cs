using MAF.InsightStreamer.Application.DTOs;

namespace MAF.InsightStreamer.Application.Interfaces;

/// <summary>
/// Defines the contract for document analysis services that orchestrate document processing and AI analysis.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Analyzes a document by extracting its content and providing AI-powered insights.
    /// </summary>
    /// <param name="fileStream">The stream containing the document data.</param>
    /// <param name="fileName">The name of the file being analyzed.</param>
    /// <param name="analysisRequest">The specific analysis request or question about the document.</param>
    /// <returns>A task that represents the asynchronous operation, containing the document analysis response.</returns>
    Task<DocumentAnalysisResponse> AnalyzeDocumentAsync(Stream fileStream, string fileName, string analysisRequest);
}