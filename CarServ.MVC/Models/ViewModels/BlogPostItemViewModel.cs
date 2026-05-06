namespace CarServ.MVC.Models.ViewModels
{
    public class BlogPostItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Thumbnail { get; set; }
        public string? ShortDescription { get; set; }
        public string? CategoryName { get; set; }
        public int? CategoryId { get; set; }
        public string? CategorySlug { get; set; }
        public DateTime? PublishedDate { get; set; }
        public int ViewCount { get; set; }
    }
}


