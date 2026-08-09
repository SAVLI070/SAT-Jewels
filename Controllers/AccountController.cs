using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SAT1.BAL;
using SAT1.Models;

namespace SAT1.Controllers
{
    public class AccountController : Controller
    {
        private readonly AuthBal _authBal;

        public AccountController(AuthBal authBal)
        {
            _authBal = authBal;
        }

        [HttpGet]
        public IActionResult SignIn()
        {
            ViewData["InitialMode"] = "signin";
            return View("Auth");
        }

        [HttpGet]
        public IActionResult SignUp()
        {
            ViewData["InitialMode"] = "signup";
            return View("Auth");
        }

        [HttpGet]
        public IActionResult Auth(string? mode = "signin")
        {
            ViewData["InitialMode"] = mode ?? "signin";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> HandleSignIn(string email, string password, bool rememberMe = false)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.ErrorMessage = "Please provide both email and password.";
                ViewData["InitialMode"] = "signin";
                return View("Auth");
            }

            var user = await _authBal.ValidateUserCredentialsAsync(email, password);
            if (user == null)
            {
                ViewBag.ErrorMessage = "Invalid credentials. Please verify your email and password.";
                ViewData["InitialMode"] = "signin";
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

            return Redirect("/");
        }

        [HttpPost]
        public async Task<IActionResult> HandleSignUp(string fullName, string email, string phone, string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.ErrorMessage = "All required fields must be filled.";
                ViewData["InitialMode"] = "signup";
                return View("Auth");
            }

            if (password != confirmPassword)
            {
                ViewBag.ErrorMessage = "Passwords do not match.";
                ViewData["InitialMode"] = "signup";
                return View("Auth");
            }

            var user = await _authBal.RegisterNewUserAsync(fullName, email, phone, password, confirmPassword);
            if (user == null)
            {
                ViewBag.ErrorMessage = "An account with this email address already exists. Please Sign In.";
                ViewData["InitialMode"] = "signup";
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

            return Redirect("/");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/");
        }
    }
}
