using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SAT1.Models;

namespace SAT1.BAL
{
    public static class DbSeeder
    {
        public static void SeedAllCollections(SatJewelDbContext db, string webRootPath)
        {
            try
            {
                var baseDir = Path.Combine(webRootPath, "assets", "ivevar");

                var collections = new List<(string Name, string Folder, string Badge, string Subtitle)>
                {
                    ("Antique Cut", "antique_cut", "Vintage", "Art Deco & Old Mine Cuts"),
                    ("Engagement Ring", "engagement_ring", "Bestseller", "Solitaires & Custom Halos"),
                    ("Eternity Ring", "eternity_ring", "Signature", "Full & Half Eternity Bands"),
                    ("Fancy Color", "fancy_color", "Rare", "Canary Yellow, Pink & Blue Diamonds"),
                    ("Nature Inspired", "nature_inspired", "Organic", "Botanical & Floral Solitaires"),
                    ("Natural Rainbow", "natural_rainbow", "Exclusive", "Multi-Color Gemstone & Diamond Bands"),
                    ("Three Stone", "three_stone", "Classic", "Past, Present & Future Rings"),
                    ("Rose Cut", "rose_cut", "Heirloom", "Flat-Backed Dome Cut Solitaires"),
                    ("Marquise Cut", "marquise_shape", "Elongated", "Navette Cut Diamond Solitaires"),
                    ("Halo", "halo_ring", "Radiant", "Micro-Pave Encircled Center Stones"),
                    ("Solitaire", "solitaire_ring", "Iconic", "Single Diamond Masterpieces"),
                    ("Anniversary Ring", "lab_diamond_anniversary_ring", "Popular", "Milestone Celebration Bands")
                };

                foreach (var cat in collections)
                {
                    var catId = cat.Name.ToLower().Replace(" ", "-");
                    var folderPath = Path.Combine(baseDir, cat.Folder);

                    List<string> images = new List<string>();
                    if (Directory.Exists(folderPath))
                    {
                        var files = Directory.GetFiles(folderPath, "*.*")
                            .Where(s => s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                        s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                        s.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                                        s.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        foreach (var f in files)
                        {
                            var rel = "/assets/ivevar/" + cat.Folder + "/" + Path.GetFileName(f);
                            images.Add(rel);
                        }
                    }

                    if (images.Count == 0)
                    {
                        images.Add("/assets/ivevar/ivevar-luxury-rings-925-silver-0-80-ct-sparkling-antique-shape-moissanite-silhouette-diamond-ring-vintage-fine-jewelry-44619076403505_c18db5d1-09b7-41c4-8fd4-f72a5a00aa4a.png");
                    }

                    var mainImage = images[0];

                    var catNameLower = cat.Name.ToLower();
                    var existingCat = db.Categories.FirstOrDefault(c => c.Id == catId || c.Name.ToLower() == catNameLower);
                    if (existingCat == null)
                    {
                        db.Categories.Add(new Category
                        {
                            Id = catId,
                            Name = cat.Name,
                            Badge = cat.Badge,
                            Subtitle = cat.Subtitle,
                            ImageUrl = mainImage,
                            DisplayOrder = 1,
                            IsActive = true
                        });
                        db.SaveChanges();
                    }

                    // Seed 4-6 distinct products for this specific category if missing
                    bool hasItems = db.CatalogItems.Any(i => i.CategoryId == catId || i.Name.Contains(cat.Name));
                    if (!hasItems)
                    {
                        var itemsToAdd = new List<CatalogItem>();
                        int count = Math.Min(50, images.Count);
                        for (int i = 0; i < count; i++)
                        {
                            var img = images[i];
                            var gallery = string.Join(",", images.Skip(i).Take(3));
                            if (string.IsNullOrWhiteSpace(gallery)) gallery = img;

                            itemsToAdd.Add(new CatalogItem
                            {
                                Id = $"sat-{catId}-{i + 1}",
                                Name = $"{cat.Name} — Bespoke Handcrafted Model #{i + 1}",
                                CategoryId = catId,
                                Spec = $"18K Gold / Platinum 950 | {1.5 + (i * 0.4):0.1}ct GIA VVS1 | {cat.Name}",
                                PriceUSD = 2400 + ((i * 380) % 3500),
                                ImageUrl = img,
                                GalleryImages = gallery,
                                MetalOptions = "18K Yellow Gold (+0)|18K White Gold (+0)|18K Rose Gold (+0)|Platinum 950 (+350)",
                                CaratOptions = "1.5ct GIA (+0)|2.0ct GIA (+750)|3.0ct GIA (+2000)",
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow
                            });
                        }

                        db.CatalogItems.AddRange(itemsToAdd);
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DbSeeder] Error seeding collections: {ex.Message}");
            }
        }
    }
}
