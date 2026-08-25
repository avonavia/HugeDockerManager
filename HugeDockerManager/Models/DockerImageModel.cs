namespace HugeDockerManager.Models;

public class DockerImageModel
{
    public string Id { get; set; } = "";
    public string Repository { get; set; } = "";
    public string Tag { get; set; } = "latest";
    public string FullName { get; set; }
    public string Size { get; set; } = "";
    public string Created { get; set; } = "";
}