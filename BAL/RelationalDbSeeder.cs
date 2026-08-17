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
                // 1. Seed Categories Hierarchy (Root + 8 Child Subcategories)
                if (!db.Categories.Any())
                {
                    var categories = new List<Category>
                    {
                        new Category { CategoryId = (long)RingCategoryEnum.Rings, Name = "Rings Collection", Slug = "rings", ParentCategoryId = null, CategoryType = "Main Category", SubCategoryName = "All Rings", DiamondType = "Lab Grown Diamond", DiamondCutShape = "All Shapes", Badge = "Top Selling", Subtitle = "Solitaires, Halos & Bands", ImageUrl = "/assets/ring_1.jpg", DisplayOrder = 1, IsActive = true },
                        new Category { CategoryId = (long)RingCategoryEnum.AnniversaryRings, Name = "Anniversary Rings", Slug = "anniversary-rings", ParentCategoryId = (long)RingCategoryEnum.Rings, CategoryType = "Sub Category", SubCategoryName = "Anniversary Ring", DiamondType = "Lab Grown Diamond", DiamondCutShape = "Oval & Princess Cut", Badge = "Popular", Subtitle = "Milestone Celebration Bands", ImageUrl = "/assets/ivevar/lab_diamond_anniversary_ring/001_ivevar-luxury-rings.jpg", DisplayOrder = 2, IsActive = true },
                        new Category { CategoryId = (long)RingCategoryEnum.RoseCutRings, Name = "Rose Cut Rings", Slug = "rose-cut-rings", ParentCategoryId = (long)RingCategoryEnum.Rings, CategoryType = "Sub Category", SubCategoryName = "Rose Cut Ring", DiamondType = "Lab Grown Diamond", DiamondCutShape = "Rose Cut", Badge = "Heirloom", Subtitle = "Flat-Backed Dome Cut Solitaires", ImageUrl = "/assets/ivevar/rose_cut/001_ivevar-luxury-rings.jpg", DisplayOrder = 3, IsActive = true },
                        new Category { CategoryId = (long)RingCategoryEnum.AntiqueCutRings, Name = "Antique Cut Rings", Slug = "antique-cut-rings", ParentCategoryId = (long)RingCategoryEnum.Rings, CategoryType = "Sub Category", SubCategoryName = "Antique Cut Ring", DiamondType = "Lab Grown Diamond", DiamondCutShape = "Old Mine Cut", Badge = "Vintage", Subtitle = "Art Deco & Old European Cuts", ImageUrl = "/assets/ivevar/antique_cut/001_ivevar-luxury-rings.jpg", DisplayOrder = 4, IsActive = true },
                        new Category { CategoryId = (long)RingCategoryEnum.EngagementRings, Name = "Engagement Rings", Slug = "engagement-rings", ParentCategoryId = (long)RingCategoryEnum.Rings, CategoryType = "Sub Category", SubCategoryName = "Engagement Ring", DiamondType = "Lab Grown Diamond", DiamondCutShape = "Radiant & Cushion", Badge = "Bestseller", Subtitle = "Solitaires & Custom Halos", ImageUrl = "/assets/ivevar/engagement_ring/001_ivevar-luxury-rings.jpg", DisplayOrder = 5, IsActive = true },
                        new Category { CategoryId = (long)RingCategoryEnum.EternityRings, Name = "Eternity Rings", Slug = "eternity-rings", ParentCategoryId = (long)RingCategoryEnum.Rings, CategoryType = "Sub Category", SubCategoryName = "Eternity Ring", DiamondType = "Lab Grown Diamond", DiamondCutShape = "Emerald & Cushion", Badge = "Signature", Subtitle = "Full & Half Eternity Bands", ImageUrl = "/assets/ivevar/eternity_ring/001_ivevar-luxury-rings.jpg", DisplayOrder = 6, IsActive = true },
                        new Category { CategoryId = (long)RingCategoryEnum.FancyColorRings, Name = "Fancy Color Diamond Rings", Slug = "fancy-color-rings", ParentCategoryId = (long)RingCategoryEnum.Rings, CategoryType = "Sub Category", SubCategoryName = "Fancy Color Ring", DiamondType = "Lab Grown Diamond", DiamondCutShape = "Canary Yellow & Pink", Badge = "Rare", Subtitle = "Canary Yellow, Pink & Blue Diamonds", ImageUrl = "/assets/ivevar/fancy_color/001_ivevar-luxury-rings.jpg", DisplayOrder = 7, IsActive = true },
                        new Category { CategoryId = (long)RingCategoryEnum.SolitaireRings, Name = "Solitaire Rings", Slug = "solitaire-rings", ParentCategoryId = (long)RingCategoryEnum.Rings, CategoryType = "Sub Category", SubCategoryName = "Solitaire Ring", DiamondType = "Lab Grown Diamond", DiamondCutShape = "Round Brilliant", Badge = "Iconic", Subtitle = "Single Diamond Masterpieces", ImageUrl = "/assets/ivevar/solitaire_ring/001_ivevar-luxury-rings.jpg", DisplayOrder = 8, IsActive = true },
                        new Category { CategoryId = (long)RingCategoryEnum.ToiEtMoiRings, Name = "Toi et Moi / Three-Stone Rings", Slug = "toi-et-moi-rings", ParentCategoryId = (long)RingCategoryEnum.Rings, CategoryType = "Sub Category", SubCategoryName = "Three Stone Ring", DiamondType = "Lab Grown Diamond", DiamondCutShape = "Pear & Emerald Dual", Badge = "Classic", Subtitle = "Past, Present & Future Rings", ImageUrl = "/assets/ivevar/three_stone/001_ivevar-luxury-rings.jpg", DisplayOrder = 9, IsActive = true }
                    };

                    db.Categories.AddRange(categories);
                    db.SaveChanges();
                }

                // 2. Seed Products with Numeric long CategoryId (Enum mapped)
                if (!db.Products.Any())
                {
                    var subcats = new List<(RingCategoryEnum EnumVal, string Slug, string Label, string Folder)>
                    {
                        (RingCategoryEnum.AnniversaryRings, "anniversary-rings", "Anniversary", "lab_diamond_anniversary_ring"),
                        (RingCategoryEnum.RoseCutRings, "rose-cut-rings", "Rose Cut", "rose_cut"),
                        (RingCategoryEnum.AntiqueCutRings, "antique-cut-rings", "Antique Cut", "antique_cut"),
                        (RingCategoryEnum.EngagementRings, "engagement-rings", "Engagement", "engagement_ring"),
                        (RingCategoryEnum.EternityRings, "eternity-rings", "Eternity", "eternity_ring"),
                        (RingCategoryEnum.FancyColorRings, "fancy-color-rings", "Fancy Color", "fancy_color"),
                        (RingCategoryEnum.SolitaireRings, "solitaire-rings", "Solitaire", "solitaire_ring"),
                        (RingCategoryEnum.ToiEtMoiRings, "toi-et-moi-rings", "Toi et Moi Three Stone", "three_stone")
                    };

                    var metalEnums = new[] { MetalTypeEnum.YellowGold14K, MetalTypeEnum.WhiteGold18K, MetalTypeEnum.RoseGold18K, MetalTypeEnum.Platinum950 };
                    var clarityEnums = new[] { DiamondClarityEnum.VVS1, DiamondClarityEnum.VVS2, DiamondClarityEnum.VS1, DiamondClarityEnum.FL };
                    var colorEnums = new[] { DiamondColorEnum.ColorD, DiamondColorEnum.ColorE, DiamondColorEnum.ColorF, DiamondColorEnum.FancyYellow };

                    int prodCount = 0;
                    foreach (var sc in subcats)
                    {
                        long numericCatId = (long)sc.EnumVal;

                        for (int i = 1; i <= 6; i++)
                        {
                            prodCount++;
                            var sku = $"SAT-{sc.Slug.Substring(0, 3).ToUpper()}-{100 + prodCount}";
                            var prodName = $"SAT Luxury {sc.Label} Diamond Ring #{i}";
                            var slug = $"sat-luxury-{sc.Slug}-{i}";
                            var price = 1450.00m + ((prodCount * 185) % 4800);
                            
                            var selectedMetalEnum = metalEnums[(i - 1) % metalEnums.Length];
                            var selectedClarityEnum = clarityEnums[(i - 1) % clarityEnums.Length];
                            var selectedColorEnum = colorEnums[(i - 1) % colorEnums.Length];

                            var metal = selectedMetalEnum.GetDisplayName();
                            var clarity = selectedClarityEnum.GetDisplayName();
                            var color = selectedColorEnum.GetDisplayName();

                            var carat = Math.Round(1.20m + (i * 0.35m), 2);
                            var desc = $"Handcrafted luxury {sc.Label} diamond ring featuring a {carat}ct {color}/{clarity} lab-grown diamond set in solid {metal}.";
                            var imgUrl = $"/assets/ivevar/{sc.Folder}/00{i}_ivevar-luxury-rings.jpg";

                            var product = new Product
                            {
                                CategoryId = numericCatId,
                                ProductName = prodName,
                                ProductSlug = slug,
                                SKU = sku,
                                BasePriceUSD = price,
                                DefaultMetalType = metal,
                                DefaultPurity = metal.Split(' ')[0],
                                DefaultCaratWeight = carat,
                                DiamondClarity = clarity,
                                DiamondColor = color,
                                Description = desc,
                                GrossWeightGram = 4.5m + (i * 0.2m),
                                IsAvailable = true,
                                CreatedAt = DateTime.UtcNow.AddMinutes(-prodCount)
                            };

                            db.Products.Add(product);
                            db.SaveChanges(); // Generates ProductId PK

                            // Primary Image
                            db.ProductImages.Add(new ProductImage
                            {
                                ProductId = product.ProductId,
                                ImageUrl = imgUrl,
                                IsMainImage = true,
                                DisplayOrder = 1
                            });

                            // Variants (US Sizes 5.0 to 9.0)
                            foreach (var rSize in new[] { "5.0", "6.0", "7.0", "8.0", "9.0" })
                            {
                                db.ProductVariants.Add(new ProductVariant
                                {
                                    ProductId = product.ProductId,
                                    RingSize = rSize,
                                    MetalType = metal,
                                    CaratWeight = carat,
                                    PriceAdjustmentUSD = rSize == "7.0" ? 0.00m : 50.00m,
                                    StockQuantity = 15,
                                    IsAvailable = true
                                });
                            }

                            // CatalogItems Compatibility Record
                            db.CatalogItems.Add(new CatalogItem
                            {
                                Id = $"sat-prod-{product.ProductId}",
                                Name = prodName,
                                CategoryId = numericCatId.ToString(),
                                Spec = $"{metal} | {carat}ct GIA {clarity} | {sc.Label}",
                                PriceUSD = price,
                                ImageUrl = imgUrl,
                                GalleryImages = imgUrl,
                                MetalOptions = "14K Yellow Gold (+0)|18K White Gold (+100)|Platinum 950 (+350)",
                                CaratOptions = "1.5ct GIA (+0)|2.0ct GIA (+750)|3.0ct GIA (+2000)",
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow.AddMinutes(-prodCount)
                            });
                        }
                    }

                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RelationalDbSeeder] Error seeding relational data: {ex.Message}");
            }
        }
    }
}
