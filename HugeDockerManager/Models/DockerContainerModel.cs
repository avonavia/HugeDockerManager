namespace HugeDockerManager.Models;

public class DockerContainerModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Unknown";
    public string Image { get; set; } = "";
    public string Status { get; set; } = "";
    public string Ports { get; set; } = "";
    public string State { get; set; } = "";
    public bool IsRunning { get; set; }
        
    public string StatusBadge => IsRunning ? "bg-success" : "bg-secondary";
    public string StatusIcon => IsRunning ? "fas fa-play-circle" : "fas fa-stop-circle";
}