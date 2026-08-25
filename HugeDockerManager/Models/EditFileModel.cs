using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HugeDockerManager.Models
{
    public class EditFileModel
    {
        [Required(ErrorMessage = "Path is required")]
        public string Path { get; set; } = "";

        [Required(ErrorMessage = "Content is required")]
        public string Content { get; set; } = "";

        [NotMapped]
        public string OriginalContent { get; set; } = "";

        public bool IsModified => Content != OriginalContent;
    }
}