using CarServ.MVC.Models;

namespace CarServ.MVC.Models.ViewModels
{
    public class BlogDetailViewModel
    {
        public BlogPost Post { get; set; } = null!;
        public IEnumerable<BlogPostItemViewModel> RelatedPosts { get; set; } = new List<BlogPostItemViewModel>();
        public IEnumerable<BlogCategory> Categories { get; set; } = new List<BlogCategory>();
    }
}


