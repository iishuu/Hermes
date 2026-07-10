namespace Hermes.Windows.AIAction;

public interface IAIActionProvider
{
    Task<AIActionProviderResult> ChatAsync(AIActionRequest request, CancellationToken cancellationToken = default);
}

public sealed record AIActionProviderResult(bool Success, string? Content, string? UserMessage)
{
    public static AIActionProviderResult Ok(string content) => new(true, content, null);

    public static AIActionProviderResult Fail(string message) => new(false, null, message);
}
