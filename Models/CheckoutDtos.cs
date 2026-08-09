namespace SAT1.Models
{
    public class CheckoutCartRequest
    {
        public List<CheckoutCartLine> Items { get; set; } = new();
    }

    public class CheckoutCartLine
    {
        public string Id { get; set; } = string.Empty;
        public string? Metal { get; set; }
        public string? Carat { get; set; }
        public string? Engraving { get; set; }
        public int Quantity { get; set; } = 1;
    }
}
