﻿using System.ComponentModel.DataAnnotations;

namespace REST_API.DTOs
{
    public class DocumentDTO
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
