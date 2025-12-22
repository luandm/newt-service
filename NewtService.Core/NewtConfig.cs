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
        if (!File.Exists(ServiceConstants.ConfigPath))
            return new NewtConfig();

        var json = File.ReadAllText(ServiceConstants.ConfigPath);
        return JsonSerializer.Deserialize<NewtConfig>(json, JsonOptions) ?? new NewtConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ServiceConstants.ConfigPath)!);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(ServiceConstants.ConfigPath, json);
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
