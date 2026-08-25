namespace Entities;

public class DockerControlRequest
{
    public string Action { get; set; } = "";
    public string ContainerId { get; set; } = "";
    public string ContainerName { get; set; } = "";
}