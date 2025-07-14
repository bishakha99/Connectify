using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Connectify.DAL;
using Microsoft.EntityFrameworkCore;
using Connectify.ViewModels;
using Connectify.DAL.Entities;

namespace Connectify.Controllers
{
    [Authorize] // Only logged-in users can access the chat
    public class ChatController : Controller
    {
        private readonly ConnectifyDb _context;

        public ChatController(ConnectifyDb context)
        {
            _context = context;
        }

        // GET: /Chat/Index
        public async Task<IActionResult> Index()
        {
            var currentUserId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var allUsers = await _context.Users
                                        .Where(u => u.Id != currentUserId)
                                        .OrderBy(u => u.Username)
                                        .ToListAsync();

            var viewModel = new ChatViewModel
            {
                CurrentUser = await _context.Users.FindAsync(currentUserId),
                AllUsers = allUsers
            };

            return View(viewModel);
        }

        // GET: /Chat/GetChatHistory?partnerId=5
        [HttpGet]
        public async Task<IActionResult> GetChatHistory(long partnerId)
        {
            var currentUserId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var messages = await _context.Messages
                .Where(m => (m.SenderId == currentUserId && m.ReceiverId == partnerId) ||
                             (m.SenderId == partnerId && m.ReceiverId == currentUserId))
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            ViewBag.CurrentUserId = currentUserId;
            return PartialView("_ChatHistory", messages);
        }

        // POST: /Chat/UploadImage
        [HttpPost]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB limit
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file selected." });

            if (!file.ContentType.StartsWith("image/"))
                return BadRequest(new { message = "Invalid file type. Only images are allowed." });

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "images");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var imageUrl = $"/uploads/images/{fileName}";
            return Ok(new { imageUrl });
        }
    }
}