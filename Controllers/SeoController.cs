using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.Controllers
{
    public class SeoController : Controller
    {
        private readonly SatJewelDbContext _context;

        public SeoController(SatJewelDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("sitemap.xml")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> SitemapXml()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            XNamespace imgNs = "http://www.google.com/schemas/sitemap-image/1.1";

            var urlElements = new List<XElement>();

            // 1. Homepage (Priority 1.0)
            urlElements.Add(new XElement(ns + "url",
                new XElement(ns + "loc", $"{baseUrl}/"),
                new XElement(ns + "lastmod", DateTime.Now.ToString("yyyy-MM-dd")),
                new XElement(ns + "changefreq", "daily"),
                new XElement(ns + "priority", "1.0")
            ));

            // 2. Main Collection & Category Pages (Priority 0.8)
            var categoryIds = new[] { 1, 2, 4, 5, 6 };
            foreach (var catId in categoryIds)
            {
                urlElements.Add(new XElement(ns + "url",
                    new XElement(ns + "loc", $"{baseUrl}/Product/Category?id={catId}"),
                    new XElement(ns + "lastmod", DateTime.Now.ToString("yyyy-MM-dd")),
                    new XElement(ns + "changefreq", "weekly"),
                    new XElement(ns + "priority", "0.8")
                ));
            }

            // 3. Static Pages & Craft Process (Priority 0.6)
            var staticPages = new[] { "/Home/CustomRings", "/Home/Index#collections", "/Home/Index#craft-video-section", "/Home/Index#ai-features", "/Home/Index#why" };
            foreach (var sp in staticPages)
            {
                urlElements.Add(new XElement(ns + "url",
                    new XElement(ns + "loc", $"{baseUrl}{sp}"),
                    new XElement(ns + "lastmod", DateTime.Now.ToString("yyyy-MM-dd")),
                    new XElement(ns + "changefreq", "monthly"),
                    new XElement(ns + "priority", "0.6")
                ));
            }

            // 4. Live Catalog Products (Priority 0.9 with Image Sitemap Tags)
            try
            {
                var products = await _context.Products
                    .Include(p => p.Images)
                    .Select(p => new {
                        p.ProductId,
                        p.Title,
                        ImagePath = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImagePath).FirstOrDefault(),
                        p.CreatedAt
                    })
                    .ToListAsync();

                foreach (var prod in products)
                {
                    var lastMod = prod.CreatedAt.ToString("yyyy-MM-dd");
                    var prodUrl = $"{baseUrl}/Product/Details/{prod.ProductId}";

                    var urlElem = new XElement(ns + "url",
                        new XElement(ns + "loc", prodUrl),
                        new XElement(ns + "lastmod", lastMod),
                        new XElement(ns + "changefreq", "weekly"),
                        new XElement(ns + "priority", "0.9")
                    );

                    if (!string.IsNullOrWhiteSpace(prod.ImagePath))
                    {
                        var imgUrl = prod.ImagePath.StartsWith("http") ? prod.ImagePath : $"{baseUrl}{prod.ImagePath}";
                        urlElem.Add(new XElement(imgNs + "image",
                            new XElement(imgNs + "loc", imgUrl),
                            new XElement(imgNs + "title", prod.Title ?? "SAT Jewel Solitaire")
                        ));
                    }

                    urlElements.Add(urlElem);
                }
            }
            catch
            {
                // Fallback if database is temporarily offline
            }

            var sitemap = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(ns + "urlset",
                    new XAttribute(XNamespace.Xmlns + "image", imgNs.NamespaceName),
                    urlElements
                )
            );

            return Content(sitemap.ToString(), "application/xml", Encoding.UTF8);
        }

        [HttpGet]
        [Route("robots.txt")]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public IActionResult RobotsTxt()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var sb = new StringBuilder();
            sb.AppendLine("User-agent: *");
            sb.AppendLine("Allow: /");
            sb.AppendLine("Disallow: /admin/");
            sb.AppendLine("Disallow: /Admin/");
            sb.AppendLine("Disallow: /Account/MyAccount");
            sb.AppendLine("Disallow: /Account/Orders");
            sb.AppendLine("Disallow: /Account/Wishlist");
            sb.AppendLine("Disallow: /Product/Cart");
            sb.AppendLine("Disallow: /Product/Checkout");
            sb.AppendLine();
            sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");

            return Content(sb.ToString(), "text/plain", Encoding.UTF8);
        }
    }
}
