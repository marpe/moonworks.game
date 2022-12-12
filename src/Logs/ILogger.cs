namespace MyGame;

public interface ILogger
{
    void LogVerbose(string str);
    void LogInfo(string str);
    void LogWarn(string str);
    void LogError(string str);
}
