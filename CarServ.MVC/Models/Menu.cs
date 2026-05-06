using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarServ.MVC.Models;

public partial class Menu
{
    public int MenuId { get; set; }

    public string MenuName { get; set; } = null!;

    public string? MenuUrl { get; set; }

    public int? ParentId { get; set; }

    public int? SortOrder { get; set; }

    public bool IsActive { get; set; }

    public string? IconClass { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual ICollection<Menu> InverseParent { get; set; } = new List<Menu>();

    public virtual Menu? Parent { get; set; }

    [NotMapped]
    public List<Menu> Children { get; set; } = new List<Menu>();
}
