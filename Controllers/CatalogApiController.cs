using System.Security.Claims;
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

        public CatalogApiController(CatalogBal catalogBal)
        {
            _catalogBal = catalogBal;
        }

        private bool IsAdminUser()
        {
            if (User.Identity?.IsAuthenticated != true) return false;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value?.ToLower() ?? "";
            return userRole == "Admin" || userEmail.Contains("admin") || User.Identity.Name == "SAT Administrator";
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

            if (category == null || string.IsNullOrWhiteSpace(category.Id))
            {
                return BadRequest(new { message = "Invalid category payload." });
            }

            var success = await _catalogBal.AddCategoryAsync(category);
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

        // 2. GET ALL CATALOG ITEMS
        [HttpGet("items")]
        public async Task<IActionResult> GetCatalogItems()
        {
            try
            {
                var items = await _catalogBal.GetAllCatalogItemsAsync();
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

            var (isValid, serverValidatedPrice, itemName, errorMsg) = await _catalogBal.CalculateServerValidatedPriceAsync(req.ItemId, req.MetalOption, req.CaratOption);

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

        // OWASP A08: SECURE MULTI-IMAGE FILE UPLOAD
        [HttpPost("upload-images")]
        public async Task<IActionResult> UploadImages([FromForm] List<IFormFile> files)
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
                return BadRequest(new { success = false, message = "Maximum 10 images allowed per item." });
            }

            const long maxFileSizeBytes = 10 * 1024 * 1024;
            var allowedExtensions = new HashSet<string> { ".jpg", ".jpeg", ".png", ".webp" };
            var allowedMimeTypes = new HashSet<string> { "image/jpeg", "image/png", "image/webp" };

            var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            var savedUrls = new List<string>();

            foreach (var file in files)
            {
                if (file.Length <= 0) continue;

                if (file.Length > maxFileSizeBytes)
                {
                    return BadRequest(new { success = false, message = $"File '{file.FileName}' exceeds the 10 MB limit." });
                }

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext))
                {
                    return BadRequest(new { success = false, message = $"Security Alert: Invalid file type '{ext}'. Only PNG, JPG, and WebP images are permitted." });
                }

                if (!allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
                {
                    return BadRequest(new { success = false, message = $"Security Alert: Invalid MIME type '{file.ContentType}'." });
                }

                using (var stream = file.OpenReadStream())
                {
                    byte[] header = new byte[8];
                    await stream.ReadAsync(header, 0, header.Length);

                    bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
                    bool isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
                    bool isWebp = header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46;

                    if (!isPng && !isJpeg && !isWebp)
                    {
                        return BadRequest(new { success = false, message = $"Security Alert: File signature check failed for '{file.FileName}'." });
                    }
                }

                var uniqueFileName = $"item_{Guid.NewGuid().ToString("N")}{ext}";
                var filePath = Path.Combine(uploadFolder, uniqueFileName);

                using (var destStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(destStream);
                }

                savedUrls.Add($"/uploads/{uniqueFileName}");
            }

            return Ok(new { success = true, count = savedUrls.Count, urls = savedUrls });
        }
    }

    public class PriceCheckRequest
    {
        public string ItemId { get; set; } = string.Empty;
        public string? MetalOption { get; set; }
        public string? CaratOption { get; set; }
    }
}
