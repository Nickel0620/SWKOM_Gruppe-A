using System.ComponentModel.DataAnnotations;

namespace DAL.Entities
{
    public class Document
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "The Title field is required.")]
        [StringLength(100, ErrorMessage = "The Title cannot exceed 100 characters.")]
        public string Title { get; set; }

        public string Content { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; } = DateTime.Now;
    }
}
