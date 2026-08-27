using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductApiController : ControllerBase
    {
        private readonly SatJewelDbContext _context;

        public ProductApiController(SatJewelDbContext context)
        {
            _context = context;
        }

        // =========================================================================
        // 1. GET /api/products
        // Full Multi-Filter Search Supporting:
        //  - categoryId
        //  - diamondShapeId / diamondShapeIds (IN...)
        //  - metalId / metalIds (IN...)
        //  - caratId / caratIds (IN...)
        //  - Pagination (limit & offset / page & pageSize)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetProducts(
            [FromQuery] long? categoryId,
            [FromQuery] long? diamondShapeId,
            [FromQuery] string? diamondShapeIds,
            [FromQuery] long? metalId,
            [FromQuery] string? metalIds,
            [FromQuery] long? caratId,
            [FromQuery] string? caratIds,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int limit = 0,
            [FromQuery] int offset = 0)
        {
            try
            {
                // Base Query with Eager Loading
                IQueryable<Product> query = _context.Products
                    .AsNoTracking()
                    .Include(p => p.Category)
                    .Include(p => p.DiamondShape)
                    .Include(p => p.Images.OrderBy(img => img.DisplayOrder));

                // A. Filter by Category ID
                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                }

                // B. Filter by Diamond Shape IDs
                if (!string.IsNullOrWhiteSpace(diamondShapeIds))
                {
                    var shapeIdList = diamondShapeIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(idStr => long.TryParse(idStr, out long parsedId) ? parsedId : 0)
                        .Where(id => id > 0)
                        .ToList();

                    if (shapeIdList.Any())
                    {
                        query = query.Where(p => shapeIdList.Contains(p.DiamondShapeId));
                    }
                }
                else if (diamondShapeId.HasValue && diamondShapeId.Value > 0)
                {
                    query = query.Where(p => p.DiamondShapeId == diamondShapeId.Value);
                }

                // C. Filter by Multi-Selected Metal IDs using IN (...)
                var metalIdList = new List<long>();
                if (!string.IsNullOrWhiteSpace(metalIds))
                {
                    metalIdList = metalIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(idStr => long.TryParse(idStr, out long parsedId) ? parsedId : 0)
                        .Where(id => id > 0)
                        .ToList();
                }
                else if (metalId.HasValue && metalId.Value > 0)
                {
                    metalIdList.Add(metalId.Value);
                }

                // D. Filter by Multi-Selected Carat IDs using IN (...)
                var caratIdList = new List<long>();
                if (!string.IsNullOrWhiteSpace(caratIds))
                {
                    caratIdList = caratIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(idStr => long.TryParse(idStr, out long parsedId) ? parsedId : 0)
                        .Where(id => id > 0)
                        .ToList();
                }
                else if (caratId.HasValue && caratId.Value > 0)
                {
                    caratIdList.Add(caratId.Value);
                }

                // Apply Metal & Carat Multi-Filters via Variants Subquery
                if (metalIdList.Any() && caratIdList.Any())
                {
                    query = query.Where(p => _context.ProductVariants.Any(v => v.ProductId == p.ProductId && metalIdList.Contains(v.MetalId) && v.CaratId.HasValue && caratIdList.Contains(v.CaratId.Value)));
                }
                else if (metalIdList.Any())
                {
                    query = query.Where(p => _context.ProductVariants.Any(v => v.ProductId == p.ProductId && metalIdList.Contains(v.MetalId)));
                }
                else if (caratIdList.Any())
                {
                    query = query.Where(p => _context.ProductVariants.Any(v => v.ProductId == p.ProductId && v.CaratId.HasValue && caratIdList.Contains(v.CaratId.Value)));
                }

                // Total Count for Pagination Metadata
                int totalItems = await query.CountAsync();

                // Calculate Pagination Limit & Offset
                int actualLimit = limit > 0 ? Math.Min(limit, 100) : Math.Clamp(pageSize, 1, 100);
                int actualOffset = offset > 0 ? offset : (Math.Max(1, page) - 1) * actualLimit;

                // Execute Paginated Query
                var products = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .ThenBy(p => p.ProductId)
                    .Skip(actualOffset)
                    .Take(actualLimit)
                    .Select(p => new
                    {
                        id = p.ProductId,
                        title = p.Title,
                        slug = p.Slug,
                        price = p.Price,
                        categoryId = p.CategoryId,
                        categoryName = p.Category != null ? p.Category.Name : string.Empty,
                        diamondShapeId = p.DiamondShapeId,
                        diamondShapeName = p.DiamondShape != null ? p.DiamondShape.Name : string.Empty,
                        diamondShapeSlug = p.DiamondShape != null ? p.DiamondShape.Slug : string.Empty,
                        mainImage = p.Images.Select(i => i.ImagePath).FirstOrDefault() ?? "/assets/hero_1.jpg",
                        images = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImagePath).ToList(),
                        createdAt = p.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    totalItems,
                    page = (actualOffset / actualLimit) + 1,
                    pageSize = actualLimit,
                    totalPages = (int)Math.Ceiling((double)totalItems / actualLimit),
                    data = products
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error fetching products", error = ex.Message });
            }
        }

        // =========================================================================
        // 2. GET /api/products/metals
        // Returns 10 Official Metals with Color Hex Swatches
        // =========================================================================
        [HttpGet("metals")]
        public async Task<IActionResult> GetMetals()
        {
            var metals = await _context.Metals
                .AsNoTracking()
                .OrderBy(m => m.Id)
                .Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    slug = m.Slug,
                    colorGroup = m.ColorGroup,
                    colorHex = m.ColorHex
                })
                .ToListAsync();

            return Ok(new { success = true, data = metals });
        }

        // =========================================================================
        // 3. GET /api/products/carat-options
        // Returns Carat Weight Options
        // =========================================================================
        [HttpGet("carat-options")]
        public async Task<IActionResult> GetCaratOptions()
        {
            var carats = await _context.CaratOptions
                .AsNoTracking()
                .OrderBy(c => c.CaratWeight)
                .Select(c => new
                {
                    id = c.Id,
                    weight = c.CaratWeight,
                    label = c.Label,
                    slug = c.Slug
                })
                .ToListAsync();

            return Ok(new { success = true, data = carats });
        }

        // =========================================================================
        // 4. GET /api/products/{id}/variant-options
        // Product Variant Selection API: Returns metal options, carat options, 
        // and full dynamic price matrix for UI metal swatch & carat selection.
        // =========================================================================
        [HttpGet("{id:long}/variant-options")]
        public async Task<IActionResult> GetProductVariantOptions(long id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Images.OrderBy(img => img.DisplayOrder))
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
            {
                return NotFound(new { success = false, message = $"Product with ID {id} not found." });
            }

            var metals = await _context.Metals.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
            var carats = await _context.CaratOptions.AsNoTracking().OrderBy(c => c.CaratWeight).ToListAsync();

            var variants = await _context.ProductVariants
                .AsNoTracking()
                .Include(v => v.Metal)
                .Include(v => v.Carat)
                .Where(v => v.ProductId == id && v.IsAvailable)
                .Select(v => new
                {
                    variantId = v.VariantId,
                    metalId = v.MetalId,
                    metalName = v.Metal != null ? v.Metal.Name : string.Empty,
                    colorHex = v.Metal != null ? v.Metal.ColorHex : string.Empty,
                    caratId = v.CaratId,
                    caratLabel = v.Carat != null ? v.Carat.Label : string.Empty,
                    caratWeight = v.Carat != null ? (decimal?)v.Carat.CaratWeight : null,
                    sku = v.SKU,
                    price = v.Price,
                    stockQuantity = v.StockQuantity,
                    imagePath = v.VariantImagePath
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                productId = product.ProductId,
                productTitle = product.Title,
                basePrice = product.Price,
                availableMetals = metals.Select(m => new { id = m.Id, name = m.Name, slug = m.Slug, colorGroup = m.ColorGroup, colorHex = m.ColorHex }),
                availableCarats = carats.Select(c => new { id = c.Id, weight = c.CaratWeight, label = c.Label, slug = c.Slug }),
                variantsCount = variants.Count,
                variants
            });
        }

        // =========================================================================
        // 5. GET /api/products/categories
        // Returns all 6 Main Categories
        // =========================================================================
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.CategoryId)
                .Select(c => new
                {
                    id = c.CategoryId,
                    name = c.Name,
                    slug = c.Slug
                })
                .ToListAsync();

            return Ok(new { success = true, data = categories });
        }

        // =========================================================================
        // 6. GET /api/products/diamond-shapes
        // Returns all 11 Diamond Shapes
        // =========================================================================
        [HttpGet("diamond-shapes")]
        public async Task<IActionResult> GetDiamondShapes()
        {
            var shapes = await _context.DiamondShapes
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    slug = s.Slug,
                    iconUrl = s.IconUrl
                })
                .ToListAsync();

            return Ok(new { success = true, data = shapes });
        }

        // =========================================================================
        // 7. GET /api/products/{id}
        // Returns single Product Details with Images, Metals, and Carat Variants
        // =========================================================================
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetProductById(long id)
        {
            var p = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.DiamondShape)
                .Include(p => p.Images.OrderBy(img => img.DisplayOrder))
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (p == null)
            {
                return NotFound(new { success = false, message = $"Product with ID {id} not found." });
            }

            var variants = await _context.ProductVariants
                .AsNoTracking()
                .Include(v => v.Metal)
                .Include(v => v.Carat)
                .Where(v => v.ProductId == id && v.IsAvailable)
                .Take(20)
                .Select(v => new
                {
                    variantId = v.VariantId,
                    metalId = v.MetalId,
                    metalName = v.Metal != null ? v.Metal.Name : string.Empty,
                    colorHex = v.Metal != null ? v.Metal.ColorHex : string.Empty,
                    caratId = v.CaratId,
                    caratLabel = v.Carat != null ? v.Carat.Label : string.Empty,
                    sku = v.SKU,
                    price = v.Price,
                    stockQuantity = v.StockQuantity
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = p.ProductId,
                    title = p.Title,
                    slug = p.Slug,
                    price = p.Price,
                    categoryId = p.CategoryId,
                    categoryName = p.Category != null ? p.Category.Name : string.Empty,
                    diamondShapeId = p.DiamondShapeId,
                    diamondShapeName = p.DiamondShape != null ? p.DiamondShape.Name : string.Empty,
                    diamondShapeSlug = p.DiamondShape != null ? p.DiamondShape.Slug : string.Empty,
                    images = p.Images.OrderBy(i => i.DisplayOrder).Select(i => new { id = i.ImageId, imagePath = i.ImagePath, displayOrder = i.DisplayOrder }).ToList(),
                    variants,
                    createdAt = p.CreatedAt
                }
            });
        }

        // =========================================================================
        // 6. GET /api/products/pricing-rules
        // Returns the dynamic database-backed metal & carat price increments
        // =========================================================================
        [HttpGet("pricing-rules")]
        public async Task<IActionResult> GetPricingRules()
        {
            var rules = await _context.DynamicPricingRules
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.RuleType)
                .ThenBy(r => r.DisplayOrder)
                .Select(r => new
                {
                    r.Id,
                    r.RuleType,
                    r.Code,
                    r.DisplayName,
                    r.PriceOffsetUSD,
                    r.DisplayOrder
                })
                .ToListAsync();

            return Ok(new { success = true, rules });
        }

        // =========================================================================
        // 7. GET /api/products/{id}/reviews
        // Returns reviews summary, average rating, star breakdown, and approved reviews
        // =========================================================================
        [HttpGet("{id}/reviews")]
        public async Task<IActionResult> GetProductReviews([FromServices] BAL.ReviewBal reviewBal, string id)
        {
            var summary = await reviewBal.GetApprovedReviewsForProductAsync(id);
            return Ok(new { success = true, data = summary });
        }

        // =========================================================================
        // 8. POST /api/products/{id}/reviews
        // Submit customer product review
        // =========================================================================
        public class SubmitReviewDto
        {
            public string ProductName { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string CustomerEmail { get; set; } = string.Empty;
            public int Rating { get; set; } = 5;
            public string ReviewTitle { get; set; } = string.Empty;
            public string ReviewText { get; set; } = string.Empty;
        }

        [HttpPost("{id}/reviews")]
        public async Task<IActionResult> SubmitProductReview([FromServices] BAL.ReviewBal reviewBal, string id, [FromBody] SubmitReviewDto dto)
        {
            var (success, message, review) = await reviewBal.SubmitCustomerReviewAsync(
                id,
                dto.ProductName,
                dto.CustomerName,
                dto.CustomerEmail,
                dto.Rating,
                dto.ReviewTitle,
                dto.ReviewText);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message, review });
        }
    }
}
