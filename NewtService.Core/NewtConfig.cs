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
        try
        {
            AppLogger.Info($"Loading config from: {ServiceConstants.ConfigPath}");
        }
        catch { }
        
        if (!File.Exists(ServiceConstants.ConfigPath))
        {
            try { AppLogger.Info("Config file does not exist"); } catch { }
            return new NewtConfig();
        }

        try
        {
            var json = File.ReadAllText(ServiceConstants.ConfigPath);
            try { AppLogger.Info($"Config loaded successfully"); } catch { }
            return JsonSerializer.Deserialize<NewtConfig>(json, JsonOptions) ?? new NewtConfig();
        }
        catch (Exception ex)
        {
            try { AppLogger.Error($"Failed to load config: {ex.Message}"); } catch { }
            return new NewtConfig();
        }
    }

    public void Save()
    {
        try { AppLogger.Info($"Saving config to: {ServiceConstants.ConfigPath}"); } catch { }
        
        try
        {
            var dir = Path.GetDirectoryName(ServiceConstants.ConfigPath)!;
            
            if (!Directory.Exists(dir))
            {
                try { AppLogger.Info($"Creating directory: {dir}"); } catch { }
                Directory.CreateDirectory(dir);
            }
            
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(ServiceConstants.ConfigPath, json);
            try { AppLogger.Info("Config saved successfully"); } catch { }
        }
        catch (Exception ex)
        {
            try { AppLogger.Error($"Failed to save config: {ex.Message}"); } catch { }
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
