using CarServ.MVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarServ.MVC.ViewComponents
{
    public class MenuTopViewComponent : ViewComponent
    {
        private readonly CarServContext _context;

        public MenuTopViewComponent(CarServContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var menus = await _context.Menus
                .Where(m => m.IsActive == true)
                .OrderBy(m => m.SortOrder)
                .ToListAsync();

            // Menu cha: ParentID = null
            var parents = menus
                .Where(m => m.ParentId == null)
                .ToList();

            // Gắn submenu
            foreach (var parent in parents)
            {
                parent.Children = menus
                    .Where(m => m.ParentId == parent.MenuId)
                    .OrderBy(m => m.SortOrder)
                    .ToList();
            }

            return View(parents);
        }
    }
}
