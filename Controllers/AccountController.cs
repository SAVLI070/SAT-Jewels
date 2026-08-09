using System.Security.Claims;
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
        private readonly SatJewelDbContext _db;

        public AccountController(AuthBal authBal, SatJewelDbContext db)
        {
            _authBal = authBal;
            _db = db;
        }

        [HttpGet]
        public IActionResult SignIn(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["InitialMode"] = "signin";
            ViewData["ReturnUrl"] = returnUrl;
            return View("Auth");
        }

        [HttpGet]
        public IActionResult SignUp(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["InitialMode"] = "signup";
            ViewData["ReturnUrl"] = returnUrl;
            return View("Auth");
        }

        [HttpGet]
        public IActionResult Auth(string? mode = "signin", string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["InitialMode"] = mode ?? "signin";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> HandleSignIn(string email, string password, bool rememberMe = false, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.ErrorMessage = "Please provide both email and password.";
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

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(8)
            });

            if (user.Role == "Admin")
            {
                return Redirect("/admin");
            }

            return RedirectToLocal(returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> HandleSignUp(string fullName, string email, string phone, string password, string confirmPassword, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.ErrorMessage = "All required fields must be filled.";
                ViewData["InitialMode"] = "signup";
                ViewData["ReturnUrl"] = returnUrl;
                return View("Auth");
            }

            if (password != confirmPassword)
            {
                ViewBag.ErrorMessage = "Passwords do not match.";
                ViewData["InitialMode"] = "signup";
                ViewData["ReturnUrl"] = returnUrl;
                return View("Auth");
            }

            var user = await _authBal.RegisterNewUserAsync(fullName, email, phone, password, confirmPassword);
            if (user == null)
            {
                ViewBag.ErrorMessage = "An account with this email address already exists. Please Sign In.";
                ViewData["InitialMode"] = "signup";
                ViewData["ReturnUrl"] = returnUrl;
                return View("Auth");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, "Client")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToLocal(returnUrl);
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

            var recentOrders = await _db.Orders
                .Where(o => o.UserId == userId || (!string.IsNullOrEmpty(email) && o.CustomerEmail == email))
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync();

            ViewBag.RecentOrders = recentOrders;
            ViewBag.FullName = User.Identity?.Name ?? "Client";
            ViewBag.Email = email;
            ViewBag.Role = User.FindFirstValue(ClaimTypes.Role) ?? "Client";
            return View();
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

            var orders = await _db.Orders
                .Where(o => o.UserId == userId || (!string.IsNullOrEmpty(email) && o.CustomerEmail == email))
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/");
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                // "/" still routes logged-in clients to Products via HomeController
                return Redirect(returnUrl);
            }

            // Default post-login destination: Products shop
            return RedirectToAction("Index", "Product");
        }
    }
}
