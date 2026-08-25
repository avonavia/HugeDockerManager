using Entities;

namespace HugeDockerManager.Models
{
    public class FileExplorerModel
    {
        public string CurrentPath { get; set; } = "";
        public List<FileItem> Items { get; set; } = new();
        public string BreadcrumbPath { get; set; } = "";
    }
}