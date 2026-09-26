using System.Security.Claims;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using SAT1.BAL;
using SAT1.Models;

namespace SAT1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CatalogApiController : ControllerBase
    {
        private readonly CatalogBal _catalogBal;
        private readonly AdminBal _adminBal;
        private readonly IConfiguration _configuration;

        public CatalogApiController(CatalogBal catalogBal, AdminBal adminBal, IConfiguration configuration)
        {
            _catalogBal = catalogBal;
            _adminBal = adminBal;
            _configuration = configuration;
        }

        private bool IsAdminUser()
        {
            return _adminBal.CheckAdminAccess(User);
        }

        // 1. GET ALL ACTIVE CATEGORIES (Dynamic Main Landing Page Grid - ONLY IsActive == true)
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categoryData = await _catalogBal.GetPublicCategoriesAsync();
                return Ok(categoryData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 1F. GET FULL STOREFRONT DATA FOR LANDING PAGE GRID (ONLY IsActive == true)
        [HttpGet("full-store")]
        public async Task<IActionResult> GetFullStore()
        {
            try
            {
                var storeData = await _catalogBal.GetFullStoreAsync();
                return Ok(storeData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 1B. GET ALL CATEGORIES FOR ADMIN PANEL (Active + Hidden)
        [HttpGet("admin-categories")]
        public async Task<IActionResult> GetAdminCategories()
        {
            try
            {
                var categoryData = await _catalogBal.GetAdminCategoriesAsync();
                return Ok(categoryData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 1C. TOGGLE CATEGORY VISIBILITY (Hide / Show on Landing Page without Deleting) - Protected (OWASP A01)
        [HttpPost("categories/{id}/toggle-visibility")]
        public async Task<IActionResult> ToggleCategoryVisibility(string id, [FromQuery] bool active)
        {
            if (!IsAdminUser())
            {
                return StatusCode(403, new { success = false, message = "Access Denied: Admin authorization required." });
            }

            var success = await _catalogBal.ToggleCategoryVisibilityAsync(id, active);
            if (!success) return NotFound(new { message = "Category not found" });

            return Ok(new { success = true, message = $"Category visibility updated to {(active ? "Visible" : "Hidden")}" });
        }

        // 1D. CREATE NEW CATEGORY - Protected (OWASP A01)
        [HttpPost("categories")]
        public async Task<IActionResult> AddCategory([FromBody] Category category)
        {
            if (!IsAdminUser())
            {
                return StatusCode(403, new { success = false, message = "Access Denied: Admin authorization required." });
            }

            if (category == null || string.IsNullOrWhiteSpace(category.Name))
            {
                return BadRequest(new { success = false, message = "Category Name is required." });
            }

            if (string.IsNullOrWhiteSpace(category.Slug))
            {
                category.Slug = category.Name.Trim().ToLower().Replace(" ", "-");
            }

            var success = await _catalogBal.AddCategoryAsync(category);
            if (!success)
            {
                return StatusCode(500, new { success = false, message = "Failed to save category to database." });
            }

            return Ok(new { success = true, message = $"Category '{category.Name}' saved successfully!" });
        }

        // 1E. DELETE CATEGORY - Protected (OWASP A01)
        [HttpDelete("categories/{id}")]
        public async Task<IActionResult> DeleteCategory(string id)
        {
            if (!IsAdminUser())
            {
                return StatusCode(403, new { success = false, message = "Access Denied: Admin authorization required." });
            }

            var success = await _catalogBal.DeleteCategoryAsync(id);
            if (!success) return NotFound(new { message = "Category not found" });

            return Ok(new { success = true, message = $"Category '{id}' deleted from database." });
        }

        // 2. GET ALL CATALOG ITEMS (With optional category filter for high performance)
        [HttpGet("items")]
        public async Task<IActionResult> GetCatalogItems([FromQuery] string? categoryId = null)
        {
            try
            {
                var items = string.IsNullOrWhiteSpace(categoryId) || categoryId == "all"
                    ? await _catalogBal.GetAllCatalogItemsAsync()
                    : await _catalogBal.GetCatalogItemsByCategoryAsync(categoryId);
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 2B. GET CATEGORY ITEMS ON-DEMAND (Ultra-fast accordion loading)
        [HttpGet("category-items/{categoryId}")]
        public async Task<IActionResult> GetCategoryItems(string categoryId)
        {
            try
            {
                var items = await _catalogBal.GetCatalogItemsByCategoryAsync(categoryId);
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 3. GET SINGLE CATALOG ITEM DETAILS BY ID
        [HttpGet("items/{id}")]
        public async Task<IActionResult> GetCatalogItem(string id)
        {
            try
            {
                var item = await _catalogBal.GetCatalogItemByIdAsync(id);
                if (item == null) return NotFound(new { message = "Jewelry product item not found." });

                return Ok(item);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 4. ADD NEW CATALOG ITEM TO NEON DB - Protected (OWASP A01)
        [HttpPost("items")]
        public async Task<IActionResult> AddCatalogItem([FromBody] CatalogItem item)
        {
            if (!IsAdminUser())
            {
                return StatusCode(403, new { success = false, message = "Access Denied: Admin authorization required." });
            }

            if (item == null) return BadRequest(new { message = "Invalid catalog item payload." });

            var saved = await _catalogBal.AddCatalogItemAsync(item);
            return Ok(new { success = true, message = $"Catalog product item '{saved.Name}' published!", item = saved });
        }

        // 4B. CREATE FULL PRODUCT WITH VARIANTS & IMAGES - Protected (OWASP A01)
        [HttpPost("create-full-product")]
        public async Task<IActionResult> CreateFullProduct([FromBody] CreateProductDto dto)
        {
            if (!IsAdminUser())
            {
                return StatusCode(403, new { success = false, message = "Access Denied: Admin authorization required." });
            }

            if (dto == null || string.IsNullOrWhiteSpace(dto.Title))
            {
                return BadRequest(new { success = false, message = "Invalid product data payload." });
            }

            try
            {
                var createdProduct = await _adminBal.CreateProductWithVariantsAsync(dto);
                return Ok(new
                {
                    success = true,
                    message = $"Product '{createdProduct.Title}' and variants published successfully to database!",
                    productId = createdProduct.ProductId
                });
            }
            catch (Exception ex)
            {
                var fullError = ex.InnerException != null ? $"{ex.Message} --> {ex.InnerException.Message}" : ex.Message;
                return StatusCode(500, new { success = false, error = fullError, message = fullError });
            }
        }

        // 5. DELETE CATALOG ITEM - Protected (OWASP A01)
        [HttpDelete("items/{id}")]
        public async Task<IActionResult> DeleteCatalogItem(string id)
        {
            if (!IsAdminUser())
            {
                return StatusCode(403, new { success = false, message = "Access Denied: Admin authorization required." });
            }

            var success = await _catalogBal.DeleteCatalogItemAsync(id);
            if (!success) return NotFound(new { message = "Catalog item not found" });

            return Ok(new { success = true, message = "Catalog item deleted successfully." });
        }

        // OWASP A01 & A04: SERVER-SIDE AUTHORITATIVE PRICE VALIDATION
        [HttpPost("validate-cart-price")]
        public async Task<IActionResult> ValidateCartPrice([FromBody] PriceCheckRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.ItemId))
            {
                return BadRequest(new { success = false, message = "Invalid product request." });
            }

            var (isValid, serverValidatedPrice, itemName, errorMsg) = await _catalogBal.CalculateServerValidatedPriceAsync(req.ItemId, req.MetalOption, req.CaratOption, req.RingSizeOption, req.StoneOption);

            if (!isValid)
            {
                return BadRequest(new { success = false, message = errorMsg });
            }

            return Ok(new
            {
                success = true,
                itemId = req.ItemId,
                itemName,
                authoritativeServerPriceUSD = serverValidatedPrice,
                currency = "USD",
                message = "Price validated against PostgreSQL database."
            });
        }

        // OWASP A08: DIRECT-TO-CLOUDINARY IMAGE FILE UPLOAD
        [HttpPost("upload-images")]
        public async Task<IActionResult> UploadImages([FromForm] List<IFormFile> files, [FromQuery] long? productId)
        {
            if (!IsAdminUser())
            {
                return StatusCode(403, new { success = false, message = "Access Denied: Admin authorization required to upload files." });
            }

            if (files == null || files.Count == 0)
            {
                return BadRequest(new { success = false, message = "No files uploaded." });
            }

            if (files.Count > 10)
            {
                return BadRequest(new { success = false, message = "Maximum 10 images allowed per upload batch." });
            }

            const long maxFileSizeBytes = 15 * 1024 * 1024; // 15 MB limit
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".jfif", ".gif" };

            var cloudName = _configuration["Cloudinary:CloudName"] ?? "ihcs8m6o";
            var apiKey = _configuration["Cloudinary:ApiKey"];
            var apiSecret = _configuration["Cloudinary:ApiSecret"];

            if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
            {
                return StatusCode(500, new { success = false, message = "Cloudinary service configuration is missing. Please check API credentials." });
            }

            var account = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
            var cloudinary = new CloudinaryDotNet.Cloudinary(account);

            var savedUrls = new List<string>();
            var folderName = productId.HasValue && productId.Value > 0 ? $"sat_jewels/{productId.Value}" : "sat_jewels/catalog_uploads";

            foreach (var file in files)
            {
                if (file.Length <= 0) continue;

                if (file.Length > maxFileSizeBytes)
                {
                    return BadRequest(new { success = false, message = $"File '{file.FileName}' exceeds the 15 MB limit." });
                }

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext))
                {
                    return BadRequest(new { success = false, message = $"Security Alert: Invalid file type '{ext}'. Supported formats: JPG, PNG, WebP." });
                }

                var ct = (file.ContentType ?? "").ToLowerInvariant();
                if (!string.IsNullOrEmpty(ct) && !ct.Contains("image") && !ct.Contains("octet-stream"))
                {
                    return BadRequest(new { success = false, message = $"Security Alert: Invalid MIME type '{file.ContentType}'." });
                }

                try
                {
                    using var memoryStream = new MemoryStream();
                    await file.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(file.FileName, memoryStream),
                        Folder = folderName,
                        Overwrite = true
                    };

                    var uploadResult = await cloudinary.UploadAsync(uploadParams);
                    var returnedUrl = uploadResult?.SecureUrl?.ToString() ?? uploadResult?.Url?.ToString();
                    if (!string.IsNullOrWhiteSpace(returnedUrl))
                    {
                        savedUrls.Add(returnedUrl);
                    }
                    else
                    {
                        return StatusCode(500, new { success = false, message = $"Cloudinary upload failed for '{file.FileName}': {uploadResult?.Error?.Message ?? "Unknown error"}" });
                    }
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { success = false, message = $"Cloudinary upload exception for '{file.FileName}': {ex.Message}" });
                }
            }

            return Ok(new { success = true, count = savedUrls.Count, urls = savedUrls });
        }

        // OWASP A08: DIRECT-TO-CLOUDINARY SINGLE IMAGE UPLOAD
        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadSingleImage([FromForm] IFormFile? file, [FromQuery] string? folder)
        {
            if (!IsAdminUser())
            {
                return StatusCode(403, new { success = false, message = "Access Denied: Admin authorization required to upload files." });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "No file provided." });
            }

            const long maxFileSizeBytes = 15 * 1024 * 1024; // 15 MB limit
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".jfif", ".gif", ".avif", ".heic", ".bmp" };

            var cloudName = _configuration["Cloudinary:CloudName"] ?? "ihcs8m6o";
            var apiKey = _configuration["Cloudinary:ApiKey"];
            var apiSecret = _configuration["Cloudinary:ApiSecret"];

            if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
            {
                return StatusCode(500, new { success = false, message = "Cloudinary configuration is missing." });
            }

            if (file.Length > maxFileSizeBytes)
            {
                return BadRequest(new { success = false, message = $"File '{file.FileName}' exceeds 15 MB." });
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                return BadRequest(new { success = false, message = "Invalid file type. Only PNG, JPG, and WebP are allowed." });
            }

            var ct = (file.ContentType ?? "").ToLowerInvariant();
            if (!string.IsNullOrEmpty(ct) && !ct.Contains("image") && !ct.Contains("octet-stream"))
            {
                return BadRequest(new { success = false, message = "Invalid MIME type. Must be an image file." });
            }

            try
            {
                var account = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
                var cloudinary = new CloudinaryDotNet.Cloudinary(account);

                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, memoryStream),
                    Folder = string.IsNullOrWhiteSpace(folder) ? "sat_jewels/categories" : folder,
                    Overwrite = true
                };

                var uploadResult = await cloudinary.UploadAsync(uploadParams);
                var returnedUrl = uploadResult?.SecureUrl?.ToString() ?? uploadResult?.Url?.ToString();
                if (!string.IsNullOrWhiteSpace(returnedUrl))
                {
                    return Ok(new { success = true, imageUrl = returnedUrl });
                }
                return StatusCode(500, new { success = false, message = uploadResult?.Error?.Message ?? "Cloudinary upload failed." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // OWASP A08: DIRECT-TO-CLOUDINARY IMAGE DELETION & STORAGE CLEANUP
        [HttpPost("delete-image")]
        [HttpDelete("delete-image")]
        public async Task<IActionResult> DeleteImage([FromQuery] string? url, [FromBody] DeleteImageRequest? body)
        {
            if (!IsAdminUser())
            {
                return StatusCode(403, new { success = false, message = "Access Denied: Admin authorization required." });
            }

            var targetUrl = url ?? body?.Url;
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                return BadRequest(new { success = false, message = "Image URL is required for deletion." });
            }

            var deleted = await _adminBal.DeleteFromCloudinaryAsync(targetUrl);
            return Ok(new { success = true, deleted = deleted, message = deleted ? "Image removed from Cloudinary storage." : "Image not found or already removed." });
        }

        // DELETE PRODUCT & CLEAN UP CLOUDINARY STORAGE
        [HttpDelete("products/{id}")]
        public async Task<IActionResult> DeleteProduct(string id)
        {
            if (!IsAdminUser())
            {
                return StatusCode(403, new { success = false, message = "Access Denied: Admin authorization required." });
            }

            var success = await _adminBal.DeleteProductAsync(id);
            if (!success) return NotFound(new { success = false, message = "Product not found" });
            return Ok(new { success = true, message = "Product and associated Cloudinary images deleted successfully." });
        }

        // 6. SEARCH PRODUCTS API FOR LIVE STOREFRONT SEARCH BAR
        [HttpGet("search")]
        public async Task<IActionResult> SearchProducts([FromQuery] string? q)
        {
            try
            {
                var queryStr = q ?? "";
                var items = await _catalogBal.SearchProductsAsync(queryStr);
                return Ok(new { success = true, query = queryStr, count = items.Count, results = items });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }

    public class PriceCheckRequest
    {
        public string ItemId { get; set; } = string.Empty;
        public string? MetalOption { get; set; }
        public string? CaratOption { get; set; }
        public string? RingSizeOption { get; set; }
        public string? StoneOption { get; set; }
    }

    public class DeleteImageRequest
    {
        public string? Url { get; set; }
    }
}
