namespace SAT1.Models
{
    public class ProductVariantMatrixItemDto
    {
        public long MetalId { get; set; }
        public long CaratId { get; set; }
        public decimal PriceOverrideUSD { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    public class CreateProductDto
    {
        public string EditId { get; set; } = "";
        public string Title { get; set; } = "";
        public decimal PriceUSD { get; set; }
        public long CategoryId { get; set; }
        public long DiamondShapeId { get; set; }
        public string DiamondType { get; set; } = "Lab Grown Diamond";
        public List<string> ImageUrls { get; set; } = new();
        public List<ProductVariantMatrixItemDto> EnabledVariants { get; set; } = new();
    }
}
