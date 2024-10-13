using System.ComponentModel.DataAnnotations;

namespace DAL.Entities
{
    public class Document
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string Content { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
