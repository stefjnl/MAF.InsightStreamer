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
}