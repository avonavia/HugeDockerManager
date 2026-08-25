namespace HugeDockerManager.Models;

public class AddFileRequest
{
    public int CategoryId { get; set; }
    public string FilePath { get; set; } = "";
}