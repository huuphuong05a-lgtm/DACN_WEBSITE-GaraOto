using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using Microsoft.AspNetCore.Authorization;

namespace CarServ.MVC.Controllers
{
    public class ContactController : Controller
    {
        private readonly CarServContext _context;

        public ContactController(CarServContext context)
        {
            _context = context;
        }

        // GET: Contact
        public IActionResult Index()
        {
            return View();
        }

        // POST: Contact/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create([Bind("FullName,Email,Phone,Subject,Message")] Contact contact)
        {
            // Get customer ID from claims
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để gửi liên hệ.";
                return RedirectToAction("Login", "Account");
            }

            // Get customer info from database
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng.";
                return RedirectToAction("Login", "Account");
            }

            // Auto-fill customer information from logged-in user
            if (string.IsNullOrEmpty(contact.FullName))
                contact.FullName = customer.FullName;
            if (string.IsNullOrEmpty(contact.Email))
                contact.Email = customer.Email;
            if (string.IsNullOrEmpty(contact.Phone))
                contact.Phone = customer.Phone;

            if (ModelState.IsValid)
            {
                contact.Status = "New";
                contact.IsRead = false;
                contact.IsReplied = false;
                contact.CreatedDate = DateTime.Now;
                contact.Ipaddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                _context.Add(contact);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Cảm ơn bạn đã liên hệ! Chúng tôi sẽ phản hồi sớm nhất có thể.";
                return RedirectToAction(nameof(Index));
            }

            return View("Index", contact);
        }
    }
}

