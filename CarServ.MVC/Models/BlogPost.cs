using System;

namespace CarServ.MVC.Models
{
    public partial class BlogPost
    {
        public int Id { get; set; }

        public string Title { get; set; } = null!;

        public string Slug { get; set; } = null!;

        public string? ShortDescription { get; set; }

        public string? Content { get; set; }

        public string? Thumbnail { get; set; }

        public int? CategoryId { get; set; }

        public DateTime? PublishedDate { get; set; }

        public bool IsPublished { get; set; }

        public int ViewCount { get; set; }

        public string? SeoTitle { get; set; }

        public string? SeoDescription { get; set; }

        public virtual BlogCategory? Category { get; set; }
    }
}


