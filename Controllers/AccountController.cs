using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAT1.BAL;
using SAT1.Models;

namespace SAT1.Controllers
{
    public class AccountController : Controller
    {
        private readonly AuthBal _authBal;
        private readonly OtpService _otpService;
        private readonly SatJewelDbContext _context;

        public AccountController(AuthBal authBal, OtpService otpService, SatJewelDbContext context)
        {
            _authBal = authBal;
            _otpService = otpService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Wishlist()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Redirect("/Account/SignIn?returnUrl=/Account/Wishlist");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Email) ?? User.Identity.Name ?? "";
            var items = await _context.WishlistItems
                .AsNoTracking()
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.AddedAt)
                .ToListAsync();

            return View(items);
        }

        [HttpGet]
        public IActionResult SignIn(string? returnUrl = null)
        {
            ViewData["InitialMode"] = "signin";
            ViewData["ReturnUrl"] = returnUrl;
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"]?.ToString();
            }
            if (TempData["PreFillEmail"] != null)
            {
                ViewBag.Email = TempData["PreFillEmail"]?.ToString();
            }
            return View("Auth");
        }

        [HttpGet]
        public IActionResult SignUp(string? returnUrl = null)
        {
            ViewData["InitialMode"] = "signup";
            ViewData["ReturnUrl"] = returnUrl;
            return View("Auth");
        }

        [HttpPost]
        public async Task<IActionResult> HandleSignIn(string email, string password, bool rememberMe = false, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.ErrorMessage = "Please enter your email and password.";
                ViewData["InitialMode"] = "signin";
                ViewData["ReturnUrl"] = returnUrl;
                return View("Auth");
            }

            var user = await _authBal.ValidateUserCredentialsAsync(email, password);
            if (user == null)
            {
                ViewBag.ErrorMessage = "Invalid credentials. Please verify your email and password.";
                ViewData["InitialMode"] = "signin";
                ViewData["ReturnUrl"] = returnUrl;
                return View("Auth");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "Client")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = null
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            if (user.Role == "Admin")
            {
                return Redirect("/admin");
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return Redirect("/");
        }

        // =========================================================================
        // FAST PHONE / OTP LOGIN (MATCHING EARTHLY JEWELS MODAL FLOW)
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> SendPhoneOtp([FromBody] PhoneOtpRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Phone))
            {
                return Json(new { success = false, message = "Please enter a valid mobile phone number." });
            }

            var result = await _otpService.GenerateAndSendOtpAsync(req.Phone);
            return Json(new
            {
                success = result.Success,
                message = result.Message,
                cooldownSeconds = result.CooldownSeconds,
                demoOtp = result.DemoOtp
            });
        }

        public class PhoneOtpRequest
        {
            public string? Phone { get; set; }
            public string? FullName { get; set; }
            public string? Otp { get; set; }
            public string? ReturnUrl { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> QuickPhoneLogin([FromBody] PhoneOtpRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Phone))
            {
                return Json(new { success = false, message = "Phone number is required." });
            }

            if (string.IsNullOrWhiteSpace(req.Otp))
            {
                return Json(new { success = false, message = "Please enter the verification code." });
            }

            var verifyResult = await _otpService.VerifyOtpAsync(req.Phone, req.Otp);
            if (!verifyResult.Success)
            {
                return Json(new { success = false, message = verifyResult.Message });
            }

            var clean = _otpService.NormalizePhoneNumber(req.Phone);
            var user = await _authBal.GetOrCreateUserByPhoneAsync(clean, req.FullName);
            if (user == null)
            {
                return Json(new { success = false, message = "Unable to establish VIP session." });
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "Client")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.Now.AddDays(30)
            });

            var targetUrl = !string.IsNullOrWhiteSpace(req.ReturnUrl) && Url.IsLocalUrl(req.ReturnUrl) ? req.ReturnUrl : "/";
            return Json(new { success = true, message = "Logged in successfully!", redirectUrl = targetUrl, userName = user.FullName });
        }

        // Real-Time Duplicate Email and Phone Availability Verification
        [HttpGet]
        public async Task<IActionResult> CheckAvailability(string? email, string? phone)
        {
            bool emailExists = false;
            bool phoneExists = false;
            string? emailMsg = null;
            string? phoneMsg = null;

            if (!string.IsNullOrWhiteSpace(email))
            {
                var cleanEmail = email.Trim().ToLower();
                emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == cleanEmail);
                if (emailExists)
                {
                    emailMsg = "An account with this email address already exists. Please Sign In.";
                }
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                var phoneDigits = Regex.Replace(phone, @"[^\d]", "");
                if (phoneDigits.Length >= 10)
                {
                    var suffix = phoneDigits.Substring(phoneDigits.Length - 10);
                    phoneExists = await _context.Users.AnyAsync(u => u.Phone != null && u.Phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("+", "").EndsWith(suffix));
                    if (phoneExists)
                    {
                        phoneMsg = "This mobile number is already registered to another account.";
                    }
                }
            }

            return Json(new { emailExists, phoneExists, emailMsg, phoneMsg });
        }

        [HttpPost]
        public async Task<IActionResult> HandleSignUp(string fullName, string email, string phone, string password, string confirmPassword, string? returnUrl = null)
        {
            ViewData["InitialMode"] = "signup";
            ViewData["ReturnUrl"] = returnUrl;
            ViewBag.FullName = fullName;
            ViewBag.Email = email;
            ViewBag.Phone = phone;

            // 1. Required Fields Validation
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.ErrorMessage = "All required fields must be filled.";
                return View("Auth");
            }

            // 2. Full Name Regex Validation (Only letters and spaces, NO numbers or special characters)
            if (!Regex.IsMatch(fullName.Trim(), @"^[a-zA-Z\s]{2,50}$"))
            {
                ViewBag.ErrorMessage = "Full Name can only contain letters and spaces (no numbers or special characters allowed).";
                return View("Auth");
            }

            // 3. Email Format Validation
            if (!Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                ViewBag.ErrorMessage = "Please enter a valid email address.";
                return View("Auth");
            }

            // 4. Duplicate Email Verification
            var cleanEmail = email.Trim().ToLower();
            var emailTaken = await _context.Users.AnyAsync(u => u.Email.ToLower() == cleanEmail);
            if (emailTaken)
            {
                ViewBag.ErrorMessage = "An account with this email address already exists. Please Sign In.";
                return View("Auth");
            }

            // 5. Mobile Phone Number Validation (Must be exactly 10 digits if provided) & Duplicate Check
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var phoneDigits = Regex.Replace(phone, @"[^\d]", "");
                if (phoneDigits.Length != 10)
                {
                    ViewBag.ErrorMessage = "Mobile number must be a valid 10-digit USA phone number (e.g. 555-123-4567).";
                    return View("Auth");
                }

                var suffix = phoneDigits.Substring(phoneDigits.Length - 10);
                var phoneTaken = await _context.Users.AnyAsync(u => u.Phone != null && u.Phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("+", "").EndsWith(suffix));
                if (phoneTaken)
                {
                    ViewBag.ErrorMessage = "This mobile number is already registered to another account. Please use another number or Sign In.";
                    return View("Auth");
                }
            }

            // 6. Password Complexity Regex Validation
            // (At least 8 characters, contains at least 1 lowercase letter, 1 letter, and 1 special character)
            if (password.Length < 8 || !Regex.IsMatch(password, @"[a-z]") || !Regex.IsMatch(password, @"[A-Za-z]") || !Regex.IsMatch(password, @"[\W_]"))
            {
                ViewBag.ErrorMessage = "Password must be at least 8 characters long and contain at least 1 lowercase letter, 1 alphabet letter, and 1 special character (e.g. @, #, $, %).";
                return View("Auth");
            }

            // 7. Confirm Password Matching
            if (password != confirmPassword)
            {
                ViewBag.ErrorMessage = "Passwords do not match. Please verify your password confirmation.";
                return View("Auth");
            }

            // 8. Safe DB Registration
            User? user = null;
            try
            {
                user = await _authBal.RegisterNewUserAsync(fullName, email, phone, password, confirmPassword);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Registration Error]: {ex.Message}");
                ViewBag.ErrorMessage = "Unable to complete registration. Please verify your information or try again.";
                return View("Auth");
            }

            if (user == null)
            {
                ViewBag.ErrorMessage = "An account with this email address already exists. Please Sign In.";
                return View("Auth");
            }

            TempData["SuccessMessage"] = "Registration successful! Please sign in with your email and password.";
            TempData["PreFillEmail"] = user.Email;

            return RedirectToAction("SignIn", new { returnUrl });
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            foreach (var cookie in Request.Cookies.Keys)
            {
                Response.Cookies.Delete(cookie);
            }

            Response.Cookies.Delete("SATJewel_AuthSession");
            Response.Cookies.Delete("SATJewel_AuthSession_v2");
            Response.Cookies.Delete("SATJewel_AuthSession_v3");

            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, max-age=0, private";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "-1";

            return View("LogoutClear");
        }

        [HttpGet]
        public async Task<IActionResult> MyAccount()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Redirect("/Account/SignIn?returnUrl=/Account/MyAccount");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email) ?? "";

            var user = await _authBal.GetUserByIdAsync(userId) ?? new User
            {
                FullName = User.Identity?.Name ?? "VIP Member",
                Email = email,
                Role = User.FindFirstValue(ClaimTypes.Role) ?? "Client"
            };

            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> Orders()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Redirect("/Account/SignIn?returnUrl=/Account/Orders");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email) ?? "";

            var orders = await _authBal.GetUserOrdersAsync(userId, email);
            return View(orders);
        }

        // =========================================================================
        // USER ADDRESS MANAGEMENT ACTIONS (ADD / EDIT / DELETE / LIST ADDRESSES)
        // =========================================================================

        [HttpGet]
        public async Task<IActionResult> Addresses()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Redirect("/Account/SignIn?returnUrl=/Account/Addresses");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var addresses = await _authBal.GetUserAddressesAsync(userId);
            return View(addresses);
        }

        [HttpPost]
        public async Task<IActionResult> SaveAddress(UserAddress model)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Redirect("/Account/SignIn?returnUrl=/Account/Addresses");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            model.UserId = userId;

            var existing = !string.IsNullOrWhiteSpace(model.AddressId) 
                ? await _authBal.GetAddressByIdAsync(model.AddressId, userId) 
                : null;

            if (existing == null)
            {
                await _authBal.AddUserAddressAsync(model);
                TempData["SuccessMessage"] = "New address successfully added to your account vault.";
            }
            else
            {
                await _authBal.UpdateUserAddressAsync(model);
                TempData["SuccessMessage"] = "Address details updated successfully.";
            }

            return RedirectToAction("Addresses");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAddress(string addressId)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Redirect("/Account/SignIn?returnUrl=/Account/Addresses");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            await _authBal.DeleteUserAddressAsync(addressId, userId);
            TempData["SuccessMessage"] = "Address deleted from your account.";

            return RedirectToAction("Addresses");
        }

        [HttpPost]
        public async Task<IActionResult> SetDefaultAddress(string addressId)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Redirect("/Account/SignIn?returnUrl=/Account/Addresses");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            await _authBal.SetDefaultUserAddressAsync(addressId, userId);
            TempData["SuccessMessage"] = "Default shipping address updated.";

            return RedirectToAction("Addresses");
        }
    }
}
