namespace NewtService.Core;

public static class ServiceConstants
{
    public const string ServiceName = "NewtVPN";
    public const string ServiceDisplayName = "Newt VPN Service";
    public const string ServiceDescription = "Windows service wrapper for Newt VPN client";

    public static string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "NewtService");

    public static string NewtExecutablePath => Path.Combine(AppDataPath, "newt.exe");
    public static string ConfigPath => Path.Combine(AppDataPath, "config.json");
    public static string LogPath => Path.Combine(AppDataPath, "logs");
    public static string VersionFilePath => Path.Combine(AppDataPath, "version.txt");

    public const string GitHubReleasesApi = "https://api.github.com/repos/fosrl/newt/releases/latest";
    public const string GitHubReleasesUrl = "https://github.com/fosrl/newt/releases";
}

