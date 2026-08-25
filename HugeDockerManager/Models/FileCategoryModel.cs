using System.ComponentModel.DataAnnotations;

namespace HugeDockerManager.Models
{
    public class FileCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<FileCategoryItem> Items { get; set; } = new();
    }

    public class FileCategoryItem
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }

    public class CreateCategoryModel
    {
        [Required, MinLength(1)]
        public string Name { get; set; } = "";
        
        public string? Description { get; set; }
    }
}