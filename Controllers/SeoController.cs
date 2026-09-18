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
            var staticPages = new[] { "/Home/CustomRings", "/Home/Index#collections", "/Home/Index#craft-video-section", "/Home/Index#why" };
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
            sb.AppendLine($"# Google Merchant Center Feed: {baseUrl}/feeds/google-merchant.xml");

            return Content(sb.ToString(), "text/plain", Encoding.UTF8);
        }

        [HttpGet]
        [Route("feeds/google-merchant.xml")]
        [Route("google-shopping-feed.xml")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> GoogleMerchantFeed()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            XNamespace g = "http://base.google.com/ns/1.0";

            var channelElements = new List<XElement>
            {
                new XElement("title", "IVEVAR Fine Jewelry - Live Google Merchant Catalog"),
                new XElement("link", $"{baseUrl}/"),
                new XElement("description", "Live jewelry catalog feed for Google Merchant Center & Google Shopping."),
                new XElement("lastBuildDate", DateTime.UtcNow.ToString("r"))
            };

            try
            {
                // 1. Fetch relational products with images
                var products = await _context.Products
                    .AsNoTracking()
                    .Include(p => p.Images)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                // 2. Fetch category names
                var categories = await _context.Categories.AsNoTracking().ToListAsync();
                var catMap = categories.ToDictionary(c => c.CategoryId, c => c.Name);

                if (products.Count > 0)
                {
                    foreach (var p in products)
                    {
                        var prodId = $"sat-prod-{p.ProductId}";
                        var title = p.ProductName;
                        var catName = catMap.GetValueOrDefault(p.CategoryId, "Engagement Rings");
                        var spec = string.IsNullOrWhiteSpace(p.Description)
                            ? $"{p.DefaultMetalType} | {p.DefaultCaratWeight}ct GIA {p.DiamondClarity} | {p.ProductName}"
                            : p.Description;
                        var prodUrl = $"{baseUrl}/Product/Details/{p.ProductId}";
                        var mainImg = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImagePath).FirstOrDefault() ?? "/assets/ring_1.jpg";
                        var fullImgUrl = mainImg.StartsWith("http") ? mainImg : $"{baseUrl}{mainImg}";
                        var priceVal = p.BasePriceUSD > 0 ? p.BasePriceUSD : 1500m;
                        var priceStr = $"{priceVal:F2} USD";

                        var itemElem = new XElement("item",
                            new XElement(g + "id", prodId),
                            new XElement(g + "title", title),
                            new XElement(g + "description", spec),
                            new XElement(g + "link", prodUrl),
                            new XElement(g + "image_link", fullImgUrl),
                            new XElement(g + "availability", "in_stock"),
                            new XElement(g + "price", priceStr),
                            new XElement(g + "brand", "IVEVAR"),
                            new XElement(g + "condition", "new"),
                            new XElement(g + "google_product_category", "188"),
                            new XElement(g + "product_type", catName),
                            new XElement(g + "identifier_exists", "no")
                        );

                        // Additional gallery images (up to 5)
                        var gallery = p.Images.OrderBy(i => i.DisplayOrder).Skip(1).Take(5).ToList();
                        foreach (var gImg in gallery)
                        {
                            if (!string.IsNullOrWhiteSpace(gImg.ImagePath))
                            {
                                var gImgUrl = gImg.ImagePath.StartsWith("http") ? gImg.ImagePath : $"{baseUrl}{gImg.ImagePath}";
                                itemElem.Add(new XElement(g + "additional_image_link", gImgUrl));
                            }
                        }

                        channelElements.Add(itemElem);
                    }
                }
                else
                {
                    // Fallback to CatalogItems
                    var catalogItems = await _context.CatalogItems
                        .AsNoTracking()
                        .Where(i => i.IsActive)
                        .OrderByDescending(i => i.CreatedAt)
                        .ToListAsync();

                    foreach (var ci in catalogItems)
                    {
                        var prodId = ci.Id;
                        var title = ci.Name;
                        var spec = string.IsNullOrWhiteSpace(ci.Spec) ? ci.Name : ci.Spec;
                        var prodUrl = $"{baseUrl}/Product/Details/{ci.Id}";
                        var fullImgUrl = string.IsNullOrWhiteSpace(ci.ImageUrl) 
                            ? $"{baseUrl}/assets/ring_1.jpg" 
                            : (ci.ImageUrl.StartsWith("http") ? ci.ImageUrl : $"{baseUrl}{ci.ImageUrl}");
                        var priceVal = ci.PriceUSD > 0 ? ci.PriceUSD : ci.Price;
                        var priceStr = $"{(priceVal > 0 ? priceVal : 1500m):F2} USD";

                        var itemElem = new XElement("item",
                            new XElement(g + "id", prodId),
                            new XElement(g + "title", title),
                            new XElement(g + "description", spec),
                            new XElement(g + "link", prodUrl),
                            new XElement(g + "image_link", fullImgUrl),
                            new XElement(g + "availability", "in_stock"),
                            new XElement(g + "price", priceStr),
                            new XElement(g + "brand", "IVEVAR"),
                            new XElement(g + "condition", "new"),
                            new XElement(g + "google_product_category", "188"),
                            new XElement(g + "product_type", ci.CategoryId ?? "Jewelry"),
                            new XElement(g + "identifier_exists", "no")
                        );

                        channelElements.Add(itemElem);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GoogleMerchantFeed Error]: {ex.Message}");
            }

            var rssDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("rss",
                    new XAttribute("version", "2.0"),
                    new XAttribute(XNamespace.Xmlns + "g", g.NamespaceName),
                    new XElement("channel", channelElements)
                )
            );

            return Content(rssDoc.ToString(), "application/xml", Encoding.UTF8);
        }
    }
}
