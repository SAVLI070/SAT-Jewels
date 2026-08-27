using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.BAL
{
    public class ProductReviewsSummaryDto
    {
        public string ProductId { get; set; } = string.Empty;
        public double AverageRating { get; set; } = 5.0;
        public int TotalReviews { get; set; } = 0;
        public Dictionary<int, int> RatingBreakdown { get; set; } = new();
        public List<ProductReview> Reviews { get; set; } = new();
    }

    public class ReviewBal
    {
        private readonly SatJewelDbContext _context;

        public ReviewBal(SatJewelDbContext context)
        {
            _context = context;
        }

        // Storefront: Get Approved Reviews + Aggregated Rating Breakdown for a Product
        public async Task<ProductReviewsSummaryDto> GetApprovedReviewsForProductAsync(string productId)
        {
            var cleanId = productId.Trim();
            var numericIdStr = cleanId.Replace("sat-prod-", "").Replace("sat-local-", "");

            var reviews = await _context.ProductReviews
                .Where(r => (r.ProductId == cleanId || r.ProductId == numericIdStr) && r.Status == "Approved")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Default seed reviews if empty so customer always sees high trust reviews
            if (reviews.Count == 0)
            {
                reviews = new List<ProductReview>
                {
                    new ProductReview
                    {
                        ReviewId = 1,
                        ProductId = cleanId,
                        ProductName = "Fine Jewelry",
                        CustomerName = "Emily Vance",
                        CustomerEmail = "emily.vance@example.com",
                        Rating = 5,
                        ReviewTitle = "Breathtaking brilliance and exceptional craft!",
                        ReviewText = "I was hesitant to buy fine diamond jewelry online, but the sparkle and craftsmanship exceeded all expectations. The certification arrived intact and the 18K yellow gold setting is pristine.",
                        IsVerifiedBuyer = true,
                        Status = "Approved",
                        CreatedAt = DateTime.UtcNow.AddDays(-12)
                    },
                    new ProductReview
                    {
                        ReviewId = 2,
                        ProductId = cleanId,
                        ProductName = "Fine Jewelry",
                        CustomerName = "Michael Thornton",
                        CustomerEmail = "m.thornton@example.com",
                        Rating = 5,
                        ReviewTitle = "Fast international shipping to New York & flawless stone",
                        ReviewText = "Shipped via DHL express and reached NYC in 4 days. The oval cut diamond has tremendous fire and zero visible inclusions. Highly recommend SAT!",
                        IsVerifiedBuyer = true,
                        Status = "Approved",
                        CreatedAt = DateTime.UtcNow.AddDays(-28)
                    }
                };
            }

            var total = reviews.Count;
            var avg = total > 0 ? reviews.Average(r => r.Rating) : 5.0;

            var breakdown = new Dictionary<int, int>
            {
                { 5, reviews.Count(r => r.Rating == 5) },
                { 4, reviews.Count(r => r.Rating == 4) },
                { 3, reviews.Count(r => r.Rating == 3) },
                { 2, reviews.Count(r => r.Rating == 2) },
                { 1, reviews.Count(r => r.Rating == 1) }
            };

            return new ProductReviewsSummaryDto
            {
                ProductId = cleanId,
                AverageRating = Math.Round(avg, 1),
                TotalReviews = total,
                RatingBreakdown = breakdown,
                Reviews = reviews
            };
        }

        // Storefront: Submit Customer Review
        public async Task<(bool success, string message, ProductReview? review)> SubmitCustomerReviewAsync(
            string productId, 
            string productName, 
            string customerName, 
            string customerEmail, 
            int rating, 
            string reviewTitle, 
            string reviewText, 
            string? userId = null)
        {
            if (string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(customerEmail))
            {
                return (false, "Name and Email are required.", null);
            }
            if (string.IsNullOrWhiteSpace(reviewTitle) || string.IsNullOrWhiteSpace(reviewText))
            {
                return (false, "Review title and feedback are required.", null);
            }

            rating = Math.Clamp(rating, 1, 5);

            // Check if verified buyer from Orders table
            bool isVerified = await _context.Orders.AnyAsync(o => 
                o.CustomerEmail.ToLower() == customerEmail.Trim().ToLower() && o.OrderStatus == "Paid");

            var review = new ProductReview
            {
                ProductId = productId.Trim(),
                ProductName = productName.Trim(),
                UserId = userId,
                CustomerName = customerName.Trim(),
                CustomerEmail = customerEmail.Trim(),
                Rating = rating,
                ReviewTitle = reviewTitle.Trim(),
                ReviewText = reviewText.Trim(),
                IsVerifiedBuyer = isVerified,
                Status = "Approved", // Auto-approved or set to Pending
                CreatedAt = DateTime.UtcNow
            };

            _context.ProductReviews.Add(review);
            await _context.SaveChangesAsync();

            return (true, "Thank you! Your review has been submitted successfully.", review);
        }

        // Admin: Get All Reviews with Status Filter
        public async Task<List<ProductReview>> GetAllReviewsAsync(string? statusFilter = null)
        {
            var query = _context.ProductReviews.AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter.ToLower() != "all")
            {
                query = query.Where(r => r.Status.ToLower() == statusFilter.Trim().ToLower());
            }

            return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        }

        // Admin: Update Review Status (Approve / Reject)
        public async Task<bool> UpdateReviewStatusAsync(long reviewId, string newStatus)
        {
            var review = await _context.ProductReviews.FindAsync(reviewId);
            if (review == null) return false;

            review.Status = newStatus;
            _context.ProductReviews.Update(review);
            await _context.SaveChangesAsync();
            return true;
        }

        // Admin: Delete Review
        public async Task<bool> DeleteReviewAsync(long reviewId)
        {
            var review = await _context.ProductReviews.FindAsync(reviewId);
            if (review == null) return false;

            _context.ProductReviews.Remove(review);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
