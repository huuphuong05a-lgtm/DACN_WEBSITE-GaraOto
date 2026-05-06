using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;

using Microsoft.AspNetCore.Authorization;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class CustomerController : Controller
    {
        private readonly CarServContext _context;

        public CustomerController(CarServContext context)
        {
            _context = context;
        }

        // GET: Admin/Customer
        public async Task<IActionResult> Index(string searchString, string statusFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";

            var customers = _context.Customers.AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                customers = customers.Where(c => 
                    c.FullName.Contains(searchString)
                    || (c.Email != null && c.Email.Contains(searchString))
                    || (c.Phone != null && c.Phone.Contains(searchString)));
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (statusFilter == "Active")
                {
                    customers = customers.Where(c => c.IsActive == true);
                }
                else if (statusFilter == "Inactive")
                {
                    customers = customers.Where(c => c.IsActive == false || c.IsActive == null);
                }
            }

            // Sort
            switch (sortOrder)
            {
                case "name_desc":
                    customers = customers.OrderByDescending(c => c.FullName);
                    break;
                case "Date":
                    customers = customers.OrderBy(c => c.CreatedDate);
                    break;
                case "date_desc":
                    customers = customers.OrderByDescending(c => c.CreatedDate);
                    break;
                default:
                    customers = customers.OrderByDescending(c => c.CreatedDate);
                    break;
            }

            var customerList = await customers.ToListAsync();
            return View(customerList);
        }

        // GET: Admin/Customer/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Orders)
                .Include(c => c.Appointments)
                .FirstOrDefaultAsync(m => m.CustomerId == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // GET: Admin/Customer/Create
        public IActionResult Create()
        {
            // Gender options
            var genderList = new[] { "Nam", "Nữ", "Khác" };
            ViewBag.GenderList = genderList;

            return View();
        }

        // POST: Admin/Customer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Email,Phone,Address,City,District,Ward,DateOfBirth,Gender,Avatar")] Customer customer)
        {
            // Handle checkboxes manually
            customer.IsActive = Request.Form["IsActive"].Contains("true");
            customer.EmailConfirmed = Request.Form["EmailConfirmed"].Contains("true");

            if (ModelState.IsValid)
            {
                customer.CreatedDate = DateTime.Now;
                customer.UpdatedDate = DateTime.Now;

                _context.Add(customer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var genderList = new[] { "Nam", "Nữ", "Khác" };
            ViewBag.GenderList = genderList;

            return View(customer);
        }

        // GET: Admin/Customer/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            // Gender options
            var genderList = new[] { "Nam", "Nữ", "Khác" };
            ViewBag.GenderList = genderList;

            return View(customer);
        }

        // POST: Admin/Customer/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CustomerId,FullName,Email,Phone,Address,City,District,Ward,DateOfBirth,Gender,Avatar,CreatedDate")] Customer customer)
        {
            if (id != customer.CustomerId)
            {
                return NotFound();
            }

            // Handle checkboxes manually
            customer.IsActive = Request.Form["IsActive"].Contains("true");
            customer.EmailConfirmed = Request.Form["EmailConfirmed"].Contains("true");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingCustomer = await _context.Customers.FindAsync(id);
                    if (existingCustomer == null)
                    {
                        return NotFound();
                    }

                    // Update properties (don't update PasswordHash, Salt, LastLoginDate)
                    existingCustomer.FullName = customer.FullName;
                    existingCustomer.Email = customer.Email;
                    existingCustomer.Phone = customer.Phone;
                    existingCustomer.Address = customer.Address;
                    existingCustomer.City = customer.City;
                    existingCustomer.District = customer.District;
                    existingCustomer.Ward = customer.Ward;
                    existingCustomer.DateOfBirth = customer.DateOfBirth;
                    existingCustomer.Gender = customer.Gender;
                    existingCustomer.Avatar = customer.Avatar;
                    existingCustomer.IsActive = customer.IsActive;
                    existingCustomer.EmailConfirmed = customer.EmailConfirmed;
                    existingCustomer.UpdatedDate = DateTime.Now;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(customer.CustomerId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            var genderList = new[] { "Nam", "Nữ", "Khác" };
            ViewBag.GenderList = genderList;

            return View(customer);
        }

        // GET: Admin/Customer/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Orders)
                .Include(c => c.Appointments)
                .FirstOrDefaultAsync(m => m.CustomerId == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // POST: Admin/Customer/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Orders)
                .Include(c => c.Appointments)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer != null)
            {
                // Check if customer has orders or appointments
                if (customer.Orders != null && customer.Orders.Any())
                {
                    TempData["ErrorMessage"] = "Không thể xóa khách hàng này vì đã có đơn hàng liên quan.";
                    return RedirectToAction(nameof(Delete), new { id = id });
                }

                if (customer.Appointments != null && customer.Appointments.Any())
                {
                    TempData["ErrorMessage"] = "Không thể xóa khách hàng này vì đã có lịch hẹn liên quan.";
                    return RedirectToAction(nameof(Delete), new { id = id });
                }

                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.CustomerId == id);
        }
    }
}

