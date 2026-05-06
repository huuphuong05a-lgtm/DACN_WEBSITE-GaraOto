using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class Product
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public string? Slug { get; set; }

    public string? Description { get; set; }

    public string? ShortDescription { get; set; }

    public decimal? Price { get; set; }

    public decimal? SalePrice { get; set; }

    public int? CategoryId { get; set; }

    public string? ImageUrl { get; set; }

    public string? GalleryImages { get; set; }

    public int? StockQuantity { get; set; }

    public string? Sku { get; set; }

    public decimal? Weight { get; set; }

    public string? Dimensions { get; set; }

    public string? Brand { get; set; }

    public int? WarrantyMonths { get; set; }

    public bool IsActive { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsNew { get; set; }

    public int? ViewCount { get; set; }

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? SupplierId { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual Category? Category { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Supplier? Supplier { get; set; }
}
