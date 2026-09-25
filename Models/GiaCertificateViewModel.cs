using System;

namespace SAT1.Models
{
    public class GiaCertificateViewModel
    {
        // Product Details
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = "Exquisite Custom Fine Jewelry";
        public string CategoryName { get; set; } = "Fine Jewelry";
        public string ImageUrl { get; set; } = "/assets/ring_1.jpg";
        public string Sku { get; set; } = "SAT-JEWEL";
        public decimal PriceUSD { get; set; }

        // Order Details (if viewed for a purchased product)
        public bool IsPurchasedOrder { get; set; } = false;
        public string? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime? OrderDate { get; set; }
        public string? ClientName { get; set; }
        public bool IncludesPhysicalCert { get; set; }

        // Certification Dossier
        public string ReportNumber { get; set; } = string.Empty;
        public string IssueDate { get; set; } = string.Empty;
        public string CertType { get; set; } = "GIA"; // GIA, GRA
        public string ReportTitle { get; set; } = "Diamond Grading Report";
        public string StoneType { get; set; } = "Lab Grown Diamond";
        public string VerificationBadge { get; set; } = "100% Verified Lab Grown Diamond";

        // 4Cs Grading
        public string CaratWeight { get; set; } = "1.50 ct";
        public decimal NumericCarat { get; set; } = 1.50m;
        public string ColorGrade { get; set; } = "E (Colorless)";
        public string ClarityGrade { get; set; } = "VVS1";
        public string CutGrade { get; set; } = "Excellent";

        // Additional Grading Info
        public string Polish { get; set; } = "Excellent";
        public string Symmetry { get; set; } = "Excellent";
        public string Fluorescence { get; set; } = "None (Faint inert)";
        public string Shape { get; set; } = "Round Brilliant";
        public string Measurements { get; set; } = "7.34 × 7.37 × 4.52 mm";

        // Proportions Analysis
        public string TablePercentage { get; set; } = "57%";
        public string DepthPercentage { get; set; } = "61.8%";
        public string CrownAngle { get; set; } = "35.0°";
        public string PavilionAngle { get; set; } = "40.8°";

        // Metal, Size & Inscription
        public string MetalType { get; set; } = "18K Gold";
        public string? RingSize { get; set; }
        public string LaserInscription { get; set; } = string.Empty;
        public string? CustomEngraving { get; set; }
    }
}
