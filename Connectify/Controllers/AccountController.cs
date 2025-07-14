using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Connectify.DAL;
using Connectify.DAL.Entities;
using Connectify.ViewModels;
using Connectify.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Connectify.Controllers
{
    public class AccountController : Controller
    {
        private readonly ConnectifyDb _context;
        private readonly IEmailService _emailService;

        public AccountController(ConnectifyDb context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Register(RegisterViewModel model)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        // Check if user already exists
        //        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        //        if (existingUser != null)
        //        {
        //            ModelState.AddModelError("Email", "An account with this email already exists.");
        //            return View(model);
        //        }

        //        // Create new user
        //        var user = new User
        //        {
        //            Username = model.Username,
        //            Email = model.Email,
        //            // Hash the password
        //            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
        //            IsVerified = false,
        //            IsActive = false,
        //            CreatedAt = DateTime.UtcNow
        //        };

        //        _context.Users.Add(user);
        //        await _context.SaveChangesAsync();

        //        // Create verification token
        //        var token = new VerificationToken
        //        {
        //            UserId = user.Id,
        //            Token = Guid.NewGuid(),
        //            ExpiresAt = DateTime.UtcNow.AddDays(1)
        //        };
        //        _context.VerificationTokens.Add(token);
        //        await _context.SaveChangesAsync();

        //        // Send verification email
        //        var verificationLink = Url.Action("VerifyEmail", "Account", new { token = token.Token }, Request.Scheme);
        //        var emailBody = $"Please verify your account by clicking this link: <a href='{verificationLink}'>Verify Email</a>";
        //        await _emailService.SendEmailAsync(user.Email, "Verify Your Connectify Account", emailBody);

        //        ViewBag.Message = "Registration successful! Please check your email to verify your account.";
        //        return View("Info"); // A simple view to show messages
        //    }
        //    return View(model);
        //}
        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "An account with this email already exists.");
                    return View(model);
                }

                var user = new User
                {
                    Username = model.Username,
                    Email = model.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    IsVerified = false,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // --- START OF OTP CHANGES ---

                // 1. Generate a 6-digit OTP
                var otp = new Random().Next(100000, 999999).ToString();

                // 2. Create and save the verification token with the OTP
                var verificationToken = new VerificationToken
                {
                    UserId = user.Id,
                    OtpCode = otp, // Save the OTP
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10) // OTPs should have a short expiry
                };
                _context.VerificationTokens.Add(verificationToken);
                await _context.SaveChangesAsync();

                // 3. Update the email body to send the OTP
                var emailBody = $"<p>Thank you for registering! Your verification code is:</p>" +
                                $"<h2 style='text-align:center; letter-spacing: 5px;'>{otp}</h2>" +
                                $"<p>This code will expire in 10 minutes.</p>";
                await _emailService.SendEmailAsync(user.Email, "Verify Your Connectify Account", emailBody);

                // 4. Redirect to the new OTP entry page, passing the user's email
                return RedirectToAction("VerifyOtp", new { email = user.Email });

                // --- END OF OTP CHANGES ---
            }
            return View(model);
        }

        // GET: /Account/VerifyEmail
        //[HttpGet]
        //public async Task<IActionResult> VerifyEmail(Guid token)
        //{
        //    var verificationToken = await _context.VerificationTokens
        //        .Include(t => t.User)
        //        .FirstOrDefaultAsync(t => t.Token == token && t.ExpiresAt > DateTime.UtcNow);

        //    if (verificationToken == null)
        //    {
        //        ViewBag.Message = "Invalid or expired verification token.";
        //        return View("Info");
        //    }

        //    var user = verificationToken.User;
        //    user.IsVerified = true;
        //    _context.Users.Update(user);

        //    // The token has been used, so remove it
        //    _context.VerificationTokens.Remove(verificationToken);

        //    await _context.SaveChangesAsync();

        //    ViewBag.Message = "Email verified successfully! You can now log in.";
        //    return View("Info");
        //}
        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

                if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }

                if (user.IsVerified != true)
                {
                    ModelState.AddModelError(string.Empty, "Your account is not verified. Please check your email.");
                    return View(model);
                }

                // Create claims
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
        };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true // Remember me
                };

                // Sign in the user
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                return RedirectToAction("Index", "Chat"); // Redirect to chat page after login
            }
            return View(model);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/VerifyOtp
        [HttpGet]
        public IActionResult VerifyOtp(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Register");
            }

            var model = new VerifyOtpViewModel { Email = email };
            ViewBag.Message = "A verification code has been sent to your email.";
            return View(model);
        }

        // POST: /Account/VerifyOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Find the most recent, unexpired token for this user's email
                var verificationToken = await _context.VerificationTokens
                    .Include(t => t.User)
                    .Where(t => t.User.Email == model.Email && t.ExpiresAt > DateTime.UtcNow)
                    .OrderByDescending(t => t.ExpiresAt)
                    .FirstOrDefaultAsync();

                if (verificationToken == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid or expired verification code. Please register again to get a new one.");
                    return View(model);
                }

                // Check if the submitted OTP matches the one in the database
                if (verificationToken.OtpCode == model.OtpCode)
                {
                    var user = verificationToken.User;
                    user.IsVerified = true;
                    _context.Users.Update(user);

                    // Remove the token now that it has been used
                    _context.VerificationTokens.Remove(verificationToken);

                    await _context.SaveChangesAsync();

                    ViewBag.Message = "Email verified successfully! You can now log in.";
                    return View("Info"); // Redirect to the success page
                }
                else
                {
                    ModelState.AddModelError("OtpCode", "The verification code is incorrect.");
                    return View(model);
                }
            }
            return View(model);
        }

    }
}