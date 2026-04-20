namespace ArbuzTweaker.Models;

public class TabInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ConfigFileName { get; set; } = string.Empty;
    public Type? TabType { get; set; }
}

public class GameConfig
{
    public string LaunchOptions { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}