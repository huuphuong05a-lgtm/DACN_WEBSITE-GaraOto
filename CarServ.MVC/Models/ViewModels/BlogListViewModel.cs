using CarServ.MVC.Models;

namespace CarServ.MVC.Models.ViewModels
{
    public class BlogListViewModel
    {
        public IEnumerable<BlogPostItemViewModel> Posts { get; set; } = new List<BlogPostItemViewModel>();
        public IEnumerable<BlogCategory> Categories { get; set; } = new List<BlogCategory>();
        public int? CurrentCategoryId { get; set; }
        public string? CurrentCategoryName { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 9;
        public int TotalPosts { get; set; }
        public string? SearchQuery { get; set; }
    }
}


