using System.Text.Json.Serialization;

namespace Entities;

public class DockerImageInfo
{
    [JsonPropertyName("ID")]
    public string ID { get; set; } = "";
    
    [JsonPropertyName("Repository")]
    public string Repository { get; set; } = "";
    
    [JsonPropertyName("Tag")]
    public string Tag { get; set; } = "";
    
    [JsonPropertyName("Size")]
    public string Size { get; set; } = "";
    
    [JsonPropertyName("CreatedSince")]
    public string CreatedSince { get; set; } = "";
    
    [JsonPropertyName("CreatedAt")]
    public string CreatedAt { get; set; } = "";
}