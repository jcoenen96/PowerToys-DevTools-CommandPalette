namespace DevTools.Application.Tools;

public sealed record LaunchResult(bool Succeeded, string Message)
{
    public static LaunchResult Success(string message) => new(true, message);

    public static LaunchResult Failure(string message) => new(false, message);
}
