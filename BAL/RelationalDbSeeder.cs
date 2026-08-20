using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.BAL
{
    public static class RelationalDbSeeder
    {
        public static void SeedRelationalData(SatJewelDbContext db)
        {
            try
            {
                // Execute SQL cleanup: Truncate all product/order/cart data while preserving table structures
                var cleanTablesSql = @"
                    TRUNCATE TABLE ""CartItems"", ""OrderItems"", ""Orders"", ""Payments"", ""ProductVariants"", ""ProductImages"", ""Products"", ""CatalogItems"" RESTART IDENTITY CASCADE;
                    TRUNCATE TABLE ""Categories"" RESTART IDENTITY CASCADE;
                ";

                try
                {
                    db.Database.ExecuteSqlRaw(cleanTablesSql);
                }
                catch (Exception exClean)
                {
                    Console.WriteLine($"[RelationalDbSeeder] SQL Truncate note: {exClean.Message}");
                    // Fallback to EF Core clear if raw truncate hits locks
                    db.OrderItems.RemoveRange(db.OrderItems);
                    db.Orders.RemoveRange(db.Orders);
                    db.Payments.RemoveRange(db.Payments);
                    db.ProductVariants.RemoveRange(db.ProductVariants);
                    db.ProductImages.RemoveRange(db.ProductImages);
                    db.Products.RemoveRange(db.Products);
                    db.CatalogItems.RemoveRange(db.CatalogItems);
                    db.Categories.RemoveRange(db.Categories);
                    db.SaveChanges();
                }

                // Seed strictly the 6 main categories starting with CategoryId 1 to 6
                var categories = new List<Category>
                {
                    new Category { CategoryId = 1, Name = "ENGAGEMENT RINGS", Slug = "engagement-rings", ParentCategoryId = null, CategoryType = "Main Category", SubCategoryName = "Engagement Ring", DiamondType = "Lab Grown Diamond", DiamondCutShape = "All Shapes", Badge = "Bestseller", Subtitle = "Solitaires & Custom Halos", ImageUrl = "/assets/hero_1.jpg", DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Category { CategoryId = 2, Name = "WEDDING RINGS", Slug = "wedding-rings", ParentCategoryId = null, CategoryType = "Main Category", SubCategoryName = "Wedding Ring", DiamondType = "Lab Grown Diamond", DiamondCutShape = "All Shapes", Badge = "Popular", Subtitle = "Eternity & Wedding Bands", ImageUrl = "/assets/ring_model.png", DisplayOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Category { CategoryId = 3, Name = "BRIDAL SETS", Slug = "bridal-sets", ParentCategoryId = null, CategoryType = "Main Category", SubCategoryName = "Bridal Set", DiamondType = "Lab Grown Diamond", DiamondCutShape = "All Shapes", Badge = "Featured", Subtitle = "Matching Engagement & Band Sets", ImageUrl = "/assets/ivevar/Bridel_set.png", DisplayOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Category { CategoryId = 4, Name = "EARRINGS", Slug = "earrings", ParentCategoryId = null, CategoryType = "Main Category", SubCategoryName = "Earrings", DiamondType = "Lab Grown Diamond", DiamondCutShape = "All Shapes", Badge = "Trending", Subtitle = "Diamond Studs & Drop Earrings", ImageUrl = "/assets/earring_card.jpg", DisplayOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Category { CategoryId = 5, Name = "BRACELETS", Slug = "bracelets", ParentCategoryId = null, CategoryType = "Main Category", SubCategoryName = "Bracelets", DiamondType = "Lab Grown Diamond", DiamondCutShape = "All Shapes", Badge = "Luxury", Subtitle = "Tennis Bracelets & Bangles", ImageUrl = "/assets/bracelet_card.jpg", DisplayOrder = 5, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Category { CategoryId = 6, Name = "NECKLACES", Slug = "necklaces", ParentCategoryId = null, CategoryType = "Main Category", SubCategoryName = "Necklaces", DiamondType = "Lab Grown Diamond", DiamondCutShape = "All Shapes", Badge = "New", Subtitle = "Pendant & Solitaire Necklaces", ImageUrl = "/assets/necklace_1.jpg", DisplayOrder = 6, IsActive = true, CreatedAt = DateTime.UtcNow }
                };

                db.Categories.AddRange(categories);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RelationalDbSeeder] Error initializing database: {ex.Message}");
            }
        }
    }
}
