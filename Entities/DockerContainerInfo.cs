using System.Text.Json.Serialization;

namespace Entities;

public class DockerContainerInfo
{
    [JsonPropertyName("ID")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("Names")]
    public string Names { get; set; } = "";
    
    [JsonPropertyName("Image")]
    public string Image { get; set; } = "";
    
    [JsonPropertyName("Status")]
    public string Status { get; set; } = "";
    
    [JsonPropertyName("Ports")]
    public string Ports { get; set; } = "";
    
    [JsonPropertyName("State")]
    public string State { get; set; } = "";
    
    [JsonPropertyName("RunningFor")]
    public string RunningFor { get; set; } = "";
}