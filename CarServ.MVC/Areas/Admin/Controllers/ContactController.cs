using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;

using Microsoft.AspNetCore.Authorization;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class ContactController : Controller
    {
        private readonly CarServContext _context;

        public ContactController(CarServContext context)
        {
            _context = context;
        }

        // GET: Admin/Contact
        public async Task<IActionResult> Index(string searchString, string statusFilter, string readFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentRead"] = readFilter;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";

            var contacts = _context.Contacts.AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                contacts = contacts.Where(c => 
                    c.FullName.Contains(searchString)
                    || (c.Email != null && c.Email.Contains(searchString))
                    || (c.Phone != null && c.Phone.Contains(searchString))
                    || (c.Subject != null && c.Subject.Contains(searchString))
                    || c.Message.Contains(searchString));
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                contacts = contacts.Where(c => c.Status == statusFilter);
            }

            // Filter by read status
            if (!string.IsNullOrEmpty(readFilter))
            {
                if (readFilter == "Read")
                {
                    contacts = contacts.Where(c => c.IsRead == true);
                }
                else if (readFilter == "Unread")
                {
                    contacts = contacts.Where(c => c.IsRead == false || c.IsRead == null);
                }
            }

            // Sort
            switch (sortOrder)
            {
                case "name_desc":
                    contacts = contacts.OrderByDescending(c => c.FullName);
                    break;
                case "Date":
                    contacts = contacts.OrderBy(c => c.CreatedDate);
                    break;
                case "date_desc":
                    contacts = contacts.OrderByDescending(c => c.CreatedDate);
                    break;
                default:
                    contacts = contacts.OrderByDescending(c => c.CreatedDate);
                    break;
            }

            var contactList = await contacts.ToListAsync();
            return View(contactList);
        }

        // GET: Admin/Contact/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contact = await _context.Contacts
                .FirstOrDefaultAsync(m => m.ContactId == id);

            if (contact == null)
            {
                return NotFound();
            }

            // Đánh dấu đã đọc khi xem chi tiết
            if (contact.IsRead != true)
            {
                contact.IsRead = true;
                if (string.IsNullOrEmpty(contact.Status) || contact.Status == "Chưa đọc")
                {
                    contact.Status = "Đã đọc";
                }
                contact.UpdatedDate = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return View(contact);
        }

        // GET: Admin/Contact/Create
        public IActionResult Create()
        {
            ViewBag.StatusList = new[] { "Chưa đọc", "Đã đọc", "Đã trả lời" };
            return View();
        }

        // POST: Admin/Contact/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Email,Phone,Subject,Message,Status,IsRead,IsReplied,ReplyMessage,RepliedBy,RepliedDate,Ipaddress")] Contact contact)
        {
            if (ModelState.IsValid)
            {
                // Xử lý nullable boolean từ checkbox
                contact.IsRead = Request.Form["IsRead"].Contains("true");
                contact.IsReplied = Request.Form["IsReplied"].Contains("true");

                if (string.IsNullOrEmpty(contact.Status))
                {
                    contact.Status = "Chưa đọc";
                }

                contact.CreatedDate = DateTime.Now;
                contact.UpdatedDate = DateTime.Now;

                _context.Add(contact);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm liên hệ thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.StatusList = new[] { "Chưa đọc", "Đã đọc", "Đã trả lời" };
            return View(contact);
        }

        // GET: Admin/Contact/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contact = await _context.Contacts.FindAsync(id);
            if (contact == null)
            {
                return NotFound();
            }

            ViewBag.StatusList = new[] { "Chưa đọc", "Đã đọc", "Đã trả lời" };
            return View(contact);
        }

        // POST: Admin/Contact/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ContactId,FullName,Email,Phone,Subject,Message,Status,IsRead,IsReplied,ReplyMessage,RepliedBy,RepliedDate,Ipaddress,CreatedDate")] Contact contact)
        {
            if (id != contact.ContactId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Xử lý nullable boolean từ checkbox
                    contact.IsRead = Request.Form["IsRead"].Contains("true");
                    contact.IsReplied = Request.Form["IsReplied"].Contains("true");

                    // Nếu có ReplyMessage và chưa có RepliedDate, set RepliedDate
                    if (!string.IsNullOrEmpty(contact.ReplyMessage) && contact.RepliedDate == null)
                    {
                        contact.RepliedDate = DateTime.Now;
                        if (string.IsNullOrEmpty(contact.RepliedBy))
                        {
                            contact.RepliedBy = User.Identity?.Name ?? "Admin";
                        }
                        contact.Status = "Đã trả lời";
                        contact.IsReplied = true;
                    }

                    contact.UpdatedDate = DateTime.Now;

                    _context.Update(contact);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật liên hệ thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ContactExists(contact.ContactId))
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

            ViewBag.StatusList = new[] { "Chưa đọc", "Đã đọc", "Đã trả lời" };
            return View(contact);
        }

        // GET: Admin/Contact/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contact = await _context.Contacts
                .FirstOrDefaultAsync(m => m.ContactId == id);

            if (contact == null)
            {
                return NotFound();
            }

            return View(contact);
        }

        // POST: Admin/Contact/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var contact = await _context.Contacts.FindAsync(id);
            if (contact != null)
            {
                _context.Contacts.Remove(contact);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa liên hệ thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Contact/MarkAsRead/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var contact = await _context.Contacts.FindAsync(id);
            if (contact != null)
            {
                contact.IsRead = true;
                if (string.IsNullOrEmpty(contact.Status) || contact.Status == "Chưa đọc")
                {
                    contact.Status = "Đã đọc";
                }
                contact.UpdatedDate = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã đánh dấu là đã đọc!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ContactExists(int id)
        {
            return _context.Contacts.Any(e => e.ContactId == id);
        }
    }
}


