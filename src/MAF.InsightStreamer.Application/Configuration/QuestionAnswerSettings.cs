namespace MAF.InsightStreamer.Application.Configuration;

/// <summary>
/// Configuration settings for question-answering functionality.
/// </summary>
public class QuestionAnswerSettings
{
    /// <summary>
    /// Gets or sets the maximum number of questions allowed per session.
    /// Default is 50 questions.
    /// </summary>
    public int MaxQuestionsPerSession { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum number of tokens allowed per session.
    /// Default is 100,000 tokens.
    /// </summary>
    public int MaxTokensPerSession { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the estimated tokens per question for budget calculation.
    /// Default is 200 tokens (approximate for average questions).
    /// </summary>
    public int EstimatedTokensPerQuestion { get; set; } = 200;

    /// <summary>
    /// Gets or sets the estimated tokens per answer for budget calculation.
    /// Default is 800 tokens (approximate for average answers).
    /// </summary>
    public int EstimatedTokensPerAnswer { get; set; } = 800;
}