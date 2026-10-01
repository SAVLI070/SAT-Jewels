using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;
using System.Text.RegularExpressions;

namespace SAT1.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly SatJewelDbContext _context;

        public HomeController(IWebHostEnvironment env, SatJewelDbContext context)
        {
            _env = env;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var dbCounts = new Dictionary<long, int>();
            try
            {
                dbCounts = await _context.Products
                    .GroupBy(p => p.CategoryId)
                    .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
            }
            catch
            {
                dbCounts = new Dictionary<long, int>();
            }

            ViewBag.CategoryCountsByNumericId = dbCounts;
            ViewData["Title"] = "Fine Jewelry & AI Diamond Intelligence — SAT Jewel";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> LandingNew()
        {
            var dbCounts = new Dictionary<long, int>();
            try
            {
                dbCounts = await _context.Products
                    .GroupBy(p => p.CategoryId)
                    .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
            }
            catch
            {
                dbCounts = new Dictionary<long, int>();
            }

            ViewBag.CategoryCountsByNumericId = dbCounts;
            ViewData["Title"] = "SAT Jewel — Fine Jewelry | Mastery in Every Cut";
            return View();
        }

        [HttpGet]
        public IActionResult About()
        {
            ViewData["Title"] = "About Us & Our Story — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult Faq()
        {
            ViewData["Title"] = "Frequently Asked Questions — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult PaymentPolicy()
        {
            ViewData["Title"] = "Payment Policy — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult PrivacyPolicy()
        {
            ViewData["Title"] = "Privacy Policy — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult RefundPolicy()
        {
            ViewData["Title"] = "Refund & Return Policy — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult ShippingPolicy()
        {
            ViewData["Title"] = "Worldwide Insured Shipping Policy — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult TermsOfService()
        {
            ViewData["Title"] = "Terms of Service & Atelier Agreement — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult CustomRings()
        {
            ViewData["Title"] = "Design Your Own Custom Engagement Ring — SAT Jewel Sanctuary";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitCustomRingInquiry(
            [FromForm] string name,
            [FromForm] string email,
            [FromForm] string phone,
            [FromForm] string category,
            [FromForm] string? metal,
            [FromForm] string? ringSize,
            [FromForm] string? budget,
            [FromForm] string? details,
            IFormFile? designImage)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phone))
                {
                    return Json(new { success = false, message = "Please provide your Name, Email, and Phone number." });
                }

                if (Regex.IsMatch(name.Trim(), @"\d"))
                {
                    return Json(new { success = false, message = "Name cannot contain numbers. Only alphabetical letters are allowed." });
                }

                var cleanPhone = System.Net.WebUtility.HtmlDecode(phone).Replace("&#x2B;", "+").Replace("&#43;", "+").Trim();
                if (Regex.IsMatch(cleanPhone, @"[a-zA-Z]"))
                {
                    return Json(new { success = false, message = "Phone number cannot contain alphabetical letters." });
                }

                string uploadedImageUrl = "";
                if (designImage != null && designImage.Length > 0)
                {
                    var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "custom_inquiries");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }
                    var ext = Path.GetExtension(designImage.FileName).ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                    var fileName = $"{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploadsDir, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await designImage.CopyToAsync(stream);
                    }
                    uploadedImageUrl = $"/uploads/custom_inquiries/{fileName}";
                }

                var inquiry = new CustomRingInquiry
                {
                    FullName = name.Trim(),
                    Email = email.Trim(),
                    Phone = phone.Trim(),
                    Category = string.IsNullOrWhiteSpace(category) ? "Custom Ring" : category.Trim(),
                    MetalPreference = metal?.Trim() ?? "14K Yellow Gold",
                    RingSize = ringSize?.Trim() ?? "US 7.0",
                    TargetBudget = budget?.Trim() ?? "",
                    Details = details?.Trim() ?? "",
                    ImageUrl = uploadedImageUrl,
                    Status = "New",
                    CreatedAt = DateTime.UtcNow
                };

                _context.CustomRingInquiries.Add(inquiry);
                await _context.SaveChangesAsync();

                return Json(new { success = true, inquiryId = inquiry.Id, message = "Your bespoke inquiry has been successfully submitted!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving request: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult CraftProcess()
        {
            return RedirectToAction("CustomRings");
        }

        [HttpGet]
        public IActionResult Blog()
        {
            ViewData["Title"] = "Jewelry Education & Lab Diamond Insights — SAT Jewel Blog";
            return View();
        }

        [HttpGet]
        public IActionResult RingSizeGuide()
        {
            ViewData["Title"] = "Find Your Ring Size Guide — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult JewelryCare()
        {
            ViewData["Title"] = "Fine Jewelry Care & Cleaning Guide — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult OrderProcess()
        {
            ViewData["Title"] = "Custom Order & Crafting Process — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult DiamondSizeChart()
        {
            ViewData["Title"] = "Carat Weight & Diamond Size Chart — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult MoissaniteVsDiamondSizeChart()
        {
            ViewData["Title"] = "Moissanite vs Diamond Size Chart — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult DiamondComparisonGuide()
        {
            ViewData["Title"] = "Moissanite vs Lab Grown Diamond vs Mined Diamond — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult Restricted()
        {
            ViewBag.Message = "The page or item you requested is not accessible directly or has been moved.";
            return View("~/Views/Shared/RestrictedAccess.cshtml");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("~/Views/Shared/RestrictedAccess.cshtml");
        }
    }
}
