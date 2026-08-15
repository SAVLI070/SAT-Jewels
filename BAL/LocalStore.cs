using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SAT1.Models;

namespace SAT1.BAL
{
    public static class LocalStore
    {
        private static readonly Dictionary<string, (string Name, string Folder, string Badge)> CategoryConfigs = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            { "anniversary-ring", ("Lab Diamond Anniversary Ring", "lab_diamond_anniversary_ring", "Popular") },
            { "anniversary ring", ("Lab Diamond Anniversary Ring", "lab_diamond_anniversary_ring", "Popular") },
            { "antique-cut", ("Antique Cut", "antique_cut", "Vintage") },
            { "antique cut", ("Antique Cut", "antique_cut", "Vintage") },
            { "engagement-ring", ("Engagement Ring", "engagement_ring", "Bestseller") },
            { "engagement ring", ("Engagement Ring", "engagement_ring", "Bestseller") },
            { "eternity-ring", ("Eternity Ring", "eternity_ring", "Signature") },
            { "eternity ring", ("Eternity Ring", "eternity_ring", "Signature") },
            { "fancy-color", ("Fancy Color", "fancy_color", "Rare") },
            { "fancy color", ("Fancy Color", "fancy_color", "Rare") },
            { "nature-inspired", ("Nature Inspired", "nature_inspired", "Organic") },
            { "nature inspired", ("Nature Inspired", "nature_inspired", "Organic") },
            { "natural-rainbow", ("Natural Rainbow", "natural_rainbow", "Exclusive") },
            { "natural rainbow", ("Natural Rainbow", "natural_rainbow", "Exclusive") },
            { "three-stone", ("Three Stone", "three_stone", "Classic") },
            { "three stone", ("Three Stone", "three_stone", "Classic") },
            { "rose-cut", ("Rose Cut", "rose_cut", "Heirloom") },
            { "rose cut", ("Rose Cut", "rose_cut", "Heirloom") },
            { "marquise-cut", ("Marquise Cut", "marquise_shape", "Elongated") },
            { "marquise cut", ("Marquise Cut", "marquise_shape", "Elongated") },
            { "halo", ("Halo", "halo_ring", "Radiant") },
            { "solitaire", ("Solitaire", "solitaire_ring", "Iconic") }
        };

        public static List<CatalogItem> GetLocalCategoryProducts(string categoryQuery, string webRootPath)
        {
            var results = new List<CatalogItem>();
            string folderName = "lab_diamond_anniversary_ring";
            string displayCategory = "Lab Diamond Anniversary Ring";

            // Match query to category folder
            var cleanKey = (categoryQuery ?? "anniversary ring").Trim().ToLower().Replace("-", " ");
            foreach (var kvp in CategoryConfigs)
            {
                if (cleanKey.Contains(kvp.Key.Replace("-", " ")) || kvp.Key.Replace("-", " ").Contains(cleanKey))
                {
                    displayCategory = kvp.Value.Name;
                    folderName = kvp.Value.Folder;
                    break;
                }
            }

            var targetFolder = Path.Combine(webRootPath, "assets", "ivevar", folderName);
            if (!Directory.Exists(targetFolder))
            {
                targetFolder = Path.Combine(webRootPath, "assets", "ivevar", "lab_diamond_anniversary_ring");
            }

            if (Directory.Exists(targetFolder))
            {
                var files = Directory.GetFiles(targetFolder, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(s => s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                s.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                                s.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Exclude loose diamond diagrams/certificates and logos so we ONLY show authentic ring product photos!
                var ringFiles = files.Where(f => {
                    var name = Path.GetFileName(f).ToLower();
                    if (name.Contains("logo") || name.Contains("icon") || name.Contains("pay") ||
                        name.Contains("badge") || name.Contains("banner") || name.Contains("whatsapp") ||
                        name.Contains("app_store") || name.Contains("play_store") || name.Contains("ask-expert") ||
                        name.Contains("return") || name.Contains("delivery") || name.Contains("guarantee"))
                        return false;

                    // Filter out loose diamond cut diagrams (CUSHION_..., MARQUISE_..., ROUND_..., etc.) if ring photos exist
                    if ((name.StartsWith("cushion_") || name.StartsWith("marquise_") || name.StartsWith("round_") ||
                         name.StartsWith("heart_") || name.StartsWith("pear_") || name.StartsWith("radiant_") ||
                         name.StartsWith("emerald_") || name.StartsWith("asscher_") || name.StartsWith("princess_")) &&
                        !name.Contains("ring") && !name.Contains("set") && !name.Contains("band"))
                        return false;

                    return true;
                }).ToList();

                // If ring files filtered too tightly, fallback to all images
                if (ringFiles.Count == 0) ringFiles = files;

                int idx = 1;
                foreach (var file in ringFiles)
                {
                    var fileName = Path.GetFileName(file);
                    var webPath = $"/assets/ivevar/{folderName}/{fileName}";

                    // Generate realistic product names and prices
                    var readableName = FormatProductName(fileName, displayCategory, idx);
                    var price = 2400 + ((idx * 370) % 3200);

                    results.Add(new CatalogItem
                    {
                        Id = $"sat-local-{folderName}-{idx}",
                        Name = readableName,
                        CategoryId = folderName,
                        Spec = $"18K Gold / Platinum 950 | {1.5 + ((idx % 5) * 0.4):0.1}ct GIA Certified | {displayCategory}",
                        PriceUSD = price,
                        ImageUrl = webPath,
                        GalleryImages = webPath,
                        MetalOptions = "18K Yellow Gold (+0)|18K White Gold (+0)|18K Rose Gold (+0)|Platinum 950 (+350)",
                        CaratOptions = "1.5ct GIA (+0)|2.0ct GIA (+750)|3.0ct GIA (+2000)",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-idx)
                    });
                    idx++;
                }
            }

            return results;
        }

        public static Dictionary<string, int> GetCategoryCounts(string webRootPath)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var folders = new[]
            {
                "lab_diamond_anniversary_ring", "antique_cut", "engagement_ring",
                "eternity_ring", "fancy_color", "nature_inspired", "natural_rainbow",
                "three_stone", "rose_cut", "marquise_shape", "halo_ring", "solitaire_ring"
            };

            foreach (var folder in folders)
            {
                var targetFolder = Path.Combine(webRootPath, "assets", "ivevar", folder);
                if (Directory.Exists(targetFolder))
                {
                    var count = Directory.GetFiles(targetFolder, "*.*")
                        .Count(s => s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                   s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                   s.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                                   s.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    counts[folder] = count > 0 ? count : 12;
                }
                else
                {
                    counts[folder] = 12;
                }
            }

            return counts;
        }

        private static string FormatProductName(string fileName, string categoryName, int index)
        {
            var clean = Path.GetFileNameWithoutExtension(fileName)
                .Replace("_", " ")
                .Replace("-", " ");

            // Remove hash UUIDs from filename if present
            if (clean.Contains(" "))
            {
                var parts = clean.Split(' ').Where(p => p.Length < 25 && !p.Any(char.IsDigit)).ToList();
                if (parts.Count > 0)
                {
                    clean = string.Join(" ", parts);
                }
            }

            if (clean.Length > 35) clean = clean.Substring(0, 35);
            clean = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(clean.ToLower());

            if (string.IsNullOrWhiteSpace(clean) || clean.Length < 4)
            {
                clean = $"{categoryName} Bespoke Model #{index}";
            }

            return clean.Contains(categoryName) ? clean : $"{categoryName} — {clean}";
        }
    }
}
