using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;
using System.Text;

namespace SAT1.Controllers
{
    public class SeoController : Controller
    {
        private readonly SatJewelDbContext _db;

        public SeoController(SatJewelDbContext db)
        {
            _db = db;
        }

        [Route("robots.txt")]
        public IActionResult RobotsTxt()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var sb = new StringBuilder();
            sb.AppendLine("User-agent: *");
            sb.AppendLine("Allow: /");
            sb.AppendLine("Disallow: /Admin/");
            sb.AppendLine("Disallow: /Account/SignIn");
            sb.AppendLine("Disallow: /Account/Orders");
            sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");

            return Content(sb.ToString(), "text/plain", Encoding.UTF8);
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> SitemapXml()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var staticPages = new List<string>
            {
                "/",
                "/Catalog",
                "/Home/OrderProcess",
                "/Home/DiamondComparisonGuide",
                "/Home/DiamondSizeChart",
                "/Home/Blog",
                "/Home/JewelryCare",
                "/Product/Cart"
            };

            var items = await _db.CatalogItems
                .AsNoTracking()
                .Select(x => new { x.Id, x.Name })
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            // Static pages
            foreach (var page in staticPages)
            {
                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{baseUrl}{page}</loc>");
                sb.AppendLine("    <changefreq>daily</changefreq>");
                sb.AppendLine("    <priority>0.9</priority>");
                sb.AppendLine("  </url>");
            }

            // Dynamic product pages
            foreach (var item in items)
            {
                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{baseUrl}/Product/Details/{item.Id}</loc>");
                sb.AppendLine("    <changefreq>weekly</changefreq>");
                sb.AppendLine("    <priority>1.0</priority>");
                sb.AppendLine("  </url>");
            }

            sb.AppendLine("</urlset>");

            return Content(sb.ToString(), "application/xml", Encoding.UTF8);
        }
    }
}
