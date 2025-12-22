using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace NewtService.Core;

public class NewtConfig
{
    public string? Endpoint { get; set; }
    public string? Id { get; set; }
    public string? Secret { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static NewtConfig Load()
    {
        AppLogger.Info($"Loading config from: {ServiceConstants.ConfigPath}");
        
        if (!File.Exists(ServiceConstants.ConfigPath))
        {
            AppLogger.Info("Config file does not exist");
            return new NewtConfig();
        }

        try
        {
            var json = File.ReadAllText(ServiceConstants.ConfigPath);
            AppLogger.Info($"Config loaded: {json}");
            return JsonSerializer.Deserialize<NewtConfig>(json, JsonOptions) ?? new NewtConfig();
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to load config: {ex.Message}");
            return new NewtConfig();
        }
    }

    public void Save()
    {
        AppLogger.Info($"Saving config to: {ServiceConstants.ConfigPath}");
        
        try
        {
            var dir = Path.GetDirectoryName(ServiceConstants.ConfigPath)!;
            
            if (!Directory.Exists(dir))
            {
                AppLogger.Info($"Creating directory: {dir}");
                var dirInfo = Directory.CreateDirectory(dir);
                try
                {
                    // Grant Users full control so both service and tray can access
                    var security = dirInfo.GetAccessControl();
                    var usersRule = new FileSystemAccessRule(
                        new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow);
                    security.AddAccessRule(usersRule);
                    dirInfo.SetAccessControl(security);
                    AppLogger.Info("Directory permissions set for Users");
                }
                catch (Exception ex)
                {
                    AppLogger.Info($"Could not set directory permissions: {ex.Message}");
                }
            }
            
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(ServiceConstants.ConfigPath, json);
            AppLogger.Info("Config saved successfully");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to save config: {ex.Message}");
            throw;
        }
    }

    public string[] BuildCommandLineArgs()
    {
        var args = new List<string>();
        
        if (!string.IsNullOrEmpty(Id))
        {
            args.Add("--id");
            args.Add(Id);
        }
        
        if (!string.IsNullOrEmpty(Secret))
        {
            args.Add("--secret");
            args.Add(Secret);
        }
        
        if (!string.IsNullOrEmpty(Endpoint))
        {
            args.Add("--endpoint");
            args.Add(Endpoint);
        }
        
        return args.ToArray();
    }
}
