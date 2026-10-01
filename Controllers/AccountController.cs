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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class AccountController : Controller
    {
        private readonly AuthBal _authBal;
        private readonly SatJewelDbContext _context;

        private static readonly Dictionary<string, string[]> UsStateZipPrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            { "California", new[] { "90", "91", "92", "93", "94", "95", "96" } },
            { "New York", new[] { "10", "11", "12", "13", "14" } },
            { "Texas", new[] { "75", "76", "77", "78", "79", "73", "88" } },
            { "Florida", new[] { "32", "33", "34" } },
            { "Illinois", new[] { "60", "61", "62" } },
            { "Pennsylvania", new[] { "15", "16", "17", "18", "19" } },
            { "Ohio", new[] { "43", "44", "45" } },
            { "Georgia", new[] { "30", "31", "39" } },
            { "North Carolina", new[] { "27", "28" } },
            { "New Jersey", new[] { "07", "08" } },
            { "Washington", new[] { "98", "99" } },
            { "Massachusetts", new[] { "01", "02" } },
            { "Nevada", new[] { "88", "89" } },
            { "Colorado", new[] { "80", "81" } },
            { "Arizona", new[] { "85", "86" } },
            { "Virginia", new[] { "20", "22", "23", "24" } },
            { "Michigan", new[] { "48", "49" } }
        };

        private static readonly Dictionary<string, string[]> IndiaStatePinPrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Gujarat", new[] { "36", "37", "38", "39" } },
            { "Maharashtra", new[] { "40", "41", "42", "43", "44" } },
            { "Delhi", new[] { "11" } },
            { "Karnataka", new[] { "56", "57", "58", "59" } },
            { "Tamil Nadu", new[] { "60", "61", "62", "63", "64" } },
            { "Telangana", new[] { "50" } },
            { "Rajasthan", new[] { "30", "31", "32", "33", "34" } },
            { "Uttar Pradesh", new[] { "20", "21", "22", "23", "24", "25", "26", "27", "28" } },
            { "West Bengal", new[] { "70", "71", "72", "73", "74" } },
            { "Punjab", new[] { "14", "15" } },
            { "Haryana", new[] { "12", "13" } },
            { "Kerala", new[] { "67", "68", "69" } },
            { "Madhya Pradesh", new[] { "45", "46", "47", "48" } },
            { "Goa", new[] { "40" } }
        };

        public static bool ValidateEmailStrict(string? email, out string errorMessage)
        {
            errorMessage = "";
            if (string.IsNullOrWhiteSpace(email))
            {
                errorMessage = "Please enter your email address.";
                return false;
            }

            var clean = email.Trim();
            if (!Regex.IsMatch(clean, @"^[a-zA-Z0-9_\-\.\+]+@[a-zA-Z0-9\-]+(\.[a-zA-Z0-9\-]+)*\.[a-zA-Z]{2,}$"))
            {
                errorMessage = "Please enter a valid email format (e.g. name@example.com).";
                return false;
            }

            var parts = clean.Split('@');
            if (parts.Length != 2)
            {
                errorMessage = "Invalid email format.";
                return false;
            }

            var local = parts[0];
            var domain = parts[1].ToLowerInvariant();

            if (domain == "gmail.com" || domain == "googlemail.com")
            {
                var stripped = local.Replace(".", "");
                if (stripped.Length < 6)
                {
                    errorMessage = "Gmail usernames must be at least 6 characters long (e.g. s@gmail.com is invalid).";
                    return false;
                }
                if (stripped.Length > 30)
                {
                    errorMessage = "Gmail usernames cannot exceed 30 characters.";
                    return false;
                }
                if (local.StartsWith(".") || local.EndsWith(".") || local.Contains(".."))
                {
                    errorMessage = "Gmail username cannot start or end with a period or contain consecutive periods.";
                    return false;
                }
            }
            else if (domain == "yahoo.com" || domain == "outlook.com" || domain == "hotmail.com" || domain == "icloud.com")
            {
                if (local.Length < 4)
                {
                    errorMessage = $"Email username must be at least 4 characters for {domain}.";
                    return false;
                }
            }
            else
            {
                if (local.Length < 3)
                {
                    errorMessage = "Email username must be at least 3 characters.";
                    return false;
                }
            }

            return true;
        }

        public static bool ValidatePhoneStrict(string? phone, bool isRequired, out string errorMessage)
        {
            errorMessage = "";
            if (string.IsNullOrWhiteSpace(phone))
            {
                if (isRequired)
                {
                    errorMessage = "Mobile phone number is required.";
                    return false;
                }
                return true;
            }

            var digits = Regex.Replace(phone, @"[^\d]", "");
            if (digits.Length < 10 || digits.Length > 15)
            {
                errorMessage = "Mobile number must be a valid 10-digit number (e.g. 555-123-4567).";
                return false;
            }

            // Reject all repeating identical digits (e.g. 1111111111, 0000000000)
            if (Regex.IsMatch(digits, @"^(\d)\1+$"))
            {
                errorMessage = "Invalid phone number: repeating dummy numbers (like 1111111111) are not allowed.";
                return false;
            }

            // Reject sequential dummy digits
            string[] sequential = { "0123456789", "1234567890", "9876543210", "0987654321" };
            if (sequential.Any(s => digits.Contains(s)))
            {
                errorMessage = "Please enter a genuine, active mobile phone number.";
                return false;
            }

            // Check 10-digit US / NANP area code
            if (digits.Length == 10)
            {
                if (digits[0] == '0' || digits[0] == '1')
                {
                    errorMessage = "US area code cannot start with 0 or 1.";
                    return false;
                }
                if (digits[3] == '0' || digits[3] == '1')
                {
                    errorMessage = "US phone exchange code cannot start with 0 or 1.";
                    return false;
                }
            }

            return true;
        }

        public AccountController(AuthBal authBal, SatJewelDbContext context)
        {
            _authBal = authBal;
            _context = context;
        }

        [HttpGet]
        public IActionResult Wishlist()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Redirect("/Account/SignIn?returnUrl=/Account/MyAccount?tab=wishlist");
            }

            return Redirect("/Account/MyAccount?tab=wishlist#wishlist");
        }

        [HttpGet]
        public IActionResult SignIn(string? returnUrl = null)
        {
            ViewData["InitialMode"] = "signin";
            ViewData["ReturnUrl"] = returnUrl;
            if (TempData["ErrorMessage"] != null)
            {
                ViewBag.ErrorMessage = TempData["ErrorMessage"]?.ToString();
            }
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
            if (TempData["ErrorMessage"] != null)
            {
                ViewBag.ErrorMessage = TempData["ErrorMessage"]?.ToString();
            }
            return View("Auth");
        }

        [HttpGet]
        public IActionResult HandleSignIn(string? returnUrl = null)
        {
            return RedirectToAction("SignIn", new { returnUrl });
        }

        [HttpGet]
        public IActionResult HandleSignUp(string? returnUrl = null)
        {
            return RedirectToAction("SignUp", new { returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HandleSignIn(string email, string password, bool rememberMe = false, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["ErrorMessage"] = "Please enter your email and password.";
                TempData["PreFillEmail"] = email;
                return RedirectToAction("SignIn", new { returnUrl });
            }

            if (!ValidateEmailStrict(email, out string emailErr))
            {
                TempData["ErrorMessage"] = emailErr;
                TempData["PreFillEmail"] = email;
                return RedirectToAction("SignIn", new { returnUrl });
            }

            var user = await _authBal.ValidateUserCredentialsAsync(email, password);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Invalid credentials. Please verify your email and password.";
                TempData["PreFillEmail"] = email;
                return RedirectToAction("SignIn", new { returnUrl });
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

            bool isAdmin = user.Role == "Admin" || 
                           user.Email.Equals("admin@satjewel.com", StringComparison.OrdinalIgnoreCase) || 
                           user.Email.Equals("admin@satjewels.com", StringComparison.OrdinalIgnoreCase) || 
                           user.Email.Equals("satjewels31@gmail.com", StringComparison.OrdinalIgnoreCase);

            if (isAdmin)
            {
                if (!string.IsNullOrWhiteSpace(returnUrl) && (Url.IsLocalUrl(returnUrl) || returnUrl.StartsWith("/")) && returnUrl.ToLower().StartsWith("/admin"))
                {
                    return Redirect(returnUrl);
                }
                return Redirect("/admin");
            }

            // Normal customer: Never redirect to /admin
            if (!string.IsNullOrWhiteSpace(returnUrl) && (Url.IsLocalUrl(returnUrl) || (returnUrl.StartsWith("/") && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\"))))
            {
                if (!returnUrl.ToLower().StartsWith("/admin"))
                {
                    return Redirect(returnUrl);
                }
            }

            return Redirect("/Account/MyAccount");
        }

        public class GoogleAuthRequest
        {
            public string? Credential { get; set; }
            public string? ReturnUrl { get; set; }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GoogleSignIn([FromBody] GoogleAuthRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Credential))
            {
                return Json(new { success = false, message = "Missing Google identity credential token." });
            }

            try
            {
                var parts = req.Credential.Split('.');
                if (parts.Length < 2)
                {
                    return Json(new { success = false, message = "Invalid credential payload format." });
                }

                var base64 = parts[1].Replace('-', '+').Replace('_', '/');
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }

                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var email = root.TryGetProperty("email", out var e) ? e.GetString() : "";
                var name = root.TryGetProperty("name", out var n) ? n.GetString() : "";
                var sub = root.TryGetProperty("sub", out var s) ? s.GetString() : "";

                if (string.IsNullOrEmpty(email))
                {
                    return Json(new { success = false, message = "Unable to retrieve verified email from Google identity." });
                }

                var user = await _authBal.GetOrCreateGoogleUserAsync(email, name ?? "Valued Client", sub ?? "");
                
                // Extract phone from Google token if present and user has no phone set yet
                var tokenPhone = root.TryGetProperty("phone_number", out var p) ? p.GetString() : (root.TryGetProperty("phone", out var ph) ? ph.GetString() : null);
                if (!string.IsNullOrWhiteSpace(tokenPhone) && string.IsNullOrWhiteSpace(user.Phone))
                {
                    user.Phone = System.Net.WebUtility.HtmlDecode(tokenPhone).Replace("&#x2B;", "+").Replace("&#43;", "+").Trim();
                    await _context.SaveChangesAsync();
                }

                bool isAdmin = user.Role == "Admin" || 
                               user.Email.Equals("admin@satjewel.com", StringComparison.OrdinalIgnoreCase) || 
                               user.Email.Equals("admin@satjewels.com", StringComparison.OrdinalIgnoreCase) || 
                               user.Email.Equals("satjewels31@gmail.com", StringComparison.OrdinalIgnoreCase);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, isAdmin ? "Admin" : (user.Role ?? "Client"))
                };

                if (!string.IsNullOrWhiteSpace(user.Phone))
                {
                    claims.Add(new Claim(ClaimTypes.MobilePhone, user.Phone));
                }

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                string redirectTarget = "/Account/MyAccount";
                if (isAdmin)
                {
                    redirectTarget = !string.IsNullOrWhiteSpace(req.ReturnUrl) && (Url.IsLocalUrl(req.ReturnUrl) || req.ReturnUrl.StartsWith("/"))
                        ? req.ReturnUrl
                        : "/admin";
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(req.ReturnUrl) && (Url.IsLocalUrl(req.ReturnUrl) || req.ReturnUrl.StartsWith("/")))
                    {
                        if (!req.ReturnUrl.ToLower().StartsWith("/admin"))
                        {
                            redirectTarget = req.ReturnUrl;
                        }
                    }
                }

                return Json(new { success = true, redirectUrl = redirectTarget, message = $"Welcome back, {user.FullName}!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Google Sign-In failed: " + ex.Message });
            }
        }



        // Real-Time Duplicate Email and Phone Availability Verification
        [HttpGet]
        public async Task<IActionResult> CheckAvailability(string? email, string? phone)
        {
            bool emailExists = false;
            bool phoneExists = false;
            bool isInvalidFormat = false;
            bool isInvalidPhone = false;
            string? emailMsg = null;
            string? phoneMsg = null;

            if (!string.IsNullOrWhiteSpace(email))
            {
                if (!ValidateEmailStrict(email, out string emailErr))
                {
                    isInvalidFormat = true;
                    emailMsg = emailErr;
                }
                else
                {
                    var cleanEmail = email.Trim().ToLower();
                    emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == cleanEmail);
                    if (emailExists)
                    {
                        emailMsg = "An account with this email address already exists. Please Sign In.";
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                if (!ValidatePhoneStrict(phone, false, out string phoneErr))
                {
                    isInvalidPhone = true;
                    phoneMsg = phoneErr;
                }
                else
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
            }

            return Json(new { emailExists, phoneExists, isInvalidFormat, isInvalidPhone, emailMsg, phoneMsg });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

            // 3. Email Format & Domain Rule Validation
            if (!ValidateEmailStrict(email, out string emailError))
            {
                ViewBag.ErrorMessage = emailError;
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

            // 5. Mobile Phone Number Validation (Must be valid phone digits, not repeating dummy) & Duplicate Check
            string cleanPhone = "";
            if (!string.IsNullOrWhiteSpace(phone))
            {
                cleanPhone = System.Net.WebUtility.HtmlDecode(phone).Replace("&#x2B;", "+").Replace("&#43;", "+").Trim();
                if (!ValidatePhoneStrict(cleanPhone, false, out string phoneError))
                {
                    ViewBag.ErrorMessage = phoneError;
                    return View("Auth");
                }

                var phoneDigits = Regex.Replace(cleanPhone, @"[^\d]", "");
                var suffix = phoneDigits.Length >= 10 ? phoneDigits.Substring(phoneDigits.Length - 10) : phoneDigits;
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
                user = await _authBal.RegisterNewUserAsync(fullName, email, cleanPhone, password, confirmPassword);
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

            // Auto sign-in new client immediately
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "Client")
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var authProperties = new AuthenticationProperties { IsPersistent = false };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            if (!string.IsNullOrWhiteSpace(returnUrl) && (Url.IsLocalUrl(returnUrl) || (returnUrl.StartsWith("/") && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\"))))
            {
                return Redirect(returnUrl);
            }

            return Redirect("/Account/MyAccount");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var deleteOptionsHttpOnly = new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(-1),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            };

            var deleteOptionsPlain = new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(-1),
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            };

            var allCookieNames = new HashSet<string>(Request.Cookies.Keys, StringComparer.OrdinalIgnoreCase)
            {
                "SATJewel_Session_v5",
                "SATJewel_AuthSession",
                "SATJewel_AuthSession_v2",
                "SATJewel_AuthSession_v3",
                "SATJewel_AuthSession_v4",
                CookieAuthenticationDefaults.AuthenticationScheme,
                ".AspNetCore.Cookies",
                ".AspNetCore.Antiforgery"
            };

            foreach (var cookie in allCookieNames)
            {
                Response.Cookies.Delete(cookie, deleteOptionsHttpOnly);
                Response.Cookies.Delete(cookie, deleteOptionsPlain);
            }

            Response.Headers["Clear-Site-Data"] = "\"cache\", \"cookies\", \"storage\"";
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

            var user = await _authBal.GetUserByIdAsync(userId);
            if (user == null && !string.IsNullOrEmpty(email))
            {
                user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            }
            if (user == null)
            {
                user = new User
                {
                    FullName = User.Identity?.Name ?? "VIP Member",
                    Email = email,
                    Role = User.FindFirstValue(ClaimTypes.Role) ?? "Client"
                };
            }

            var orders = await _authBal.GetUserOrdersAsync(userId, email);
            var addresses = await _authBal.GetUserAddressesAsync(userId ?? "");
            var wishlist = await _context.WishlistItems
                .AsNoTracking()
                .Where(w => w.UserId == userId || (!string.IsNullOrEmpty(email) && w.UserId == email))
                .OrderByDescending(w => w.AddedAt)
                .ToListAsync();

            ViewBag.Orders = orders;
            ViewBag.Addresses = addresses;
            ViewBag.Wishlist = wishlist;

            return View(user);
        }

        [HttpGet]
        public IActionResult Orders()
        {
            return Redirect("/Account/MyAccount?tab=orders#orders");
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Redirect("/Account/SignIn?returnUrl=/Account/EditProfile");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email) ?? "";

            var user = await _authBal.GetUserByIdAsync(userId);
            if (user == null && !string.IsNullOrEmpty(email))
            {
                user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            }
            if (user == null)
            {
                user = new User
                {
                    FullName = User.Identity?.Name ?? "VIP Member",
                    Email = email,
                    Role = User.FindFirstValue(ClaimTypes.Role) ?? "Client"
                };
            }

            return View(user);
        }

        public class UpdateProfileRequest
        {
            public string? FullName { get; set; }
            public string? Phone { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized(new { success = false, message = "Not authenticated" });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null && !string.IsNullOrEmpty(userEmail))
            {
                user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == userEmail.ToLower());
            }

            if (user == null)
            {
                return BadRequest(new { success = false, message = "User not found" });
            }
            if (user != null)
            {
                if (!string.IsNullOrWhiteSpace(req?.FullName))
                {
                    var cleanName = req.FullName.Trim();
                    if (!Regex.IsMatch(cleanName, @"^[a-zA-Z\s\.\-']+$"))
                    {
                        return BadRequest(new { success = false, message = "Full Name cannot contain numbers. Only alphabetical letters are allowed." });
                    }
                    user.FullName = cleanName;
                }
                if (!string.IsNullOrWhiteSpace(req?.Phone))
                {
                    var cleanPhone = System.Net.WebUtility.HtmlDecode(req.Phone).Replace("&#x2B;", "+").Replace("&#43;", "+").Trim();
                    if (Regex.IsMatch(cleanPhone, @"[a-zA-Z]"))
                    {
                        return BadRequest(new { success = false, message = "Phone number cannot contain alphabetical letters." });
                    }
                    user.Phone = cleanPhone;
                }
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, message = "Profile updated successfully." });
        }

        // =========================================================================
        // USER ADDRESS MANAGEMENT ACTIONS (ADD / EDIT / DELETE / LIST ADDRESSES)
        // =========================================================================

        [HttpGet]
        public IActionResult Addresses()
        {
            return Redirect("/Account/MyAccount?tab=addresses#addresses");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAddress(UserAddress model)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Redirect("/Account/SignIn?returnUrl=/Account/Addresses");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            model.UserId = userId;

            // Strict alphabet validation for FullName & City
            if (!string.IsNullOrWhiteSpace(model.FullName))
            {
                var cleanName = model.FullName.Trim();
                if (!Regex.IsMatch(cleanName, @"^[a-zA-Z\s\.\-']+$"))
                {
                    TempData["ErrorMessage"] = "Recipient Full Name cannot contain numbers. Only alphabetical letters are allowed.";
                    return Redirect("/Account/MyAccount?tab=addresses#addresses");
                }
                model.FullName = cleanName;
            }
            if (!string.IsNullOrWhiteSpace(model.City))
            {
                var cleanCity = model.City.Trim();
                if (!Regex.IsMatch(cleanCity, @"^[a-zA-Z\s\.\-']+$"))
                {
                    TempData["ErrorMessage"] = "City cannot contain numbers. Only alphabetical letters are allowed.";
                    return Redirect("/Account/MyAccount?tab=addresses#addresses");
                }
                model.City = cleanCity;
            }
            if (!string.IsNullOrWhiteSpace(model.Phone))
            {
                model.Phone = System.Net.WebUtility.HtmlDecode(model.Phone).Replace("&#x2B;", "+").Replace("&#43;", "+").Trim();
                if (Regex.IsMatch(model.Phone, @"[a-zA-Z]"))
                {
                    TempData["ErrorMessage"] = "Phone number cannot contain alphabetical letters.";
                    return Redirect("/Account/MyAccount?tab=addresses#addresses");
                }
            }

            // Validate ZIP / Postal Code strictly against Country & State
            if (string.Equals(model.Country, "United States", StringComparison.OrdinalIgnoreCase))
            {
                var postal = (model.PostalCode ?? "").Trim();
                if (!Regex.IsMatch(postal, @"^\d{5}(-\d{4})?$"))
                {
                    TempData["ErrorMessage"] = "Please enter a valid 5-digit US ZIP code.";
                    return Redirect("/Account/MyAccount?tab=addresses#addresses");
                }
                var prefix = postal.Length >= 2 ? postal.Substring(0, 2) : "";
                if (!string.IsNullOrWhiteSpace(model.State) && UsStateZipPrefixes.TryGetValue(model.State.Trim(), out var allowed))
                {
                    if (!allowed.Contains(prefix))
                    {
                        var foundState = UsStateZipPrefixes.FirstOrDefault(kvp => kvp.Value.Contains(prefix)).Key;
                        TempData["ErrorMessage"] = foundState != null
                            ? $"ZIP code {postal} belongs to {foundState}, not {model.State}."
                            : $"ZIP code {postal} does not match state of {model.State}.";
                        return Redirect("/Account/MyAccount?tab=addresses#addresses");
                    }
                }
            }
            else if (string.Equals(model.Country, "India", StringComparison.OrdinalIgnoreCase))
            {
                var postal = (model.PostalCode ?? "").Trim();
                if (!Regex.IsMatch(postal, @"^\d{6}$"))
                {
                    TempData["ErrorMessage"] = "Please enter a valid 6-digit Indian PIN code.";
                    return Redirect("/Account/MyAccount?tab=addresses#addresses");
                }
                var prefix = postal.Length >= 2 ? postal.Substring(0, 2) : "";
                if (!string.IsNullOrWhiteSpace(model.State) && IndiaStatePinPrefixes.TryGetValue(model.State.Trim(), out var allowed))
                {
                    if (!allowed.Contains(prefix))
                    {
                        var foundState = IndiaStatePinPrefixes.FirstOrDefault(kvp => kvp.Value.Contains(prefix)).Key;
                        TempData["ErrorMessage"] = foundState != null
                            ? $"PIN code {postal} belongs to {foundState}, not {model.State}."
                            : $"PIN code {postal} does not match state of {model.State}.";
                        return Redirect("/Account/MyAccount?tab=addresses#addresses");
                    }
                }
            }

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
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
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
