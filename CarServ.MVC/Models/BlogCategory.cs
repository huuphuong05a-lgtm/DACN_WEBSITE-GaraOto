using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models
{
    public partial class BlogCategory
    {
        public BlogCategory()
        {
            BlogPosts = new HashSet<BlogPost>();
        }

        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string Slug { get; set; } = null!;

        public string? Description { get; set; }

        public bool IsActive { get; set; }

        public int DisplayOrder { get; set; }

        public DateTime CreatedDate { get; set; }

        public virtual ICollection<BlogPost> BlogPosts { get; set; }
    }
}


