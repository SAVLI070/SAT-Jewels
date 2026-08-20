using System.ComponentModel.DataAnnotations;

namespace SAT1.Models
{
    public enum RingCategoryEnum : long
    {
        [Display(Name = "Engagement Rings")]
        EngagementRings = 1,

        [Display(Name = "Wedding Rings")]
        WeddingRings = 2,

        [Display(Name = "Bridal Sets")]
        BridalSets = 3,

        [Display(Name = "Earrings")]
        Earrings = 4,

        [Display(Name = "Bracelets")]
        Bracelets = 5,

        [Display(Name = "Necklaces")]
        Necklaces = 6,

        // Legacy Enum Aliases mapping to primary 6 categories
        AnniversaryRings = 1,
        RoseCutRings = 1,
        AntiqueCutRings = 1,
        EternityRings = 2,
        FancyColorRings = 1,
        SolitaireRings = 1,
        ToiEtMoiRings = 1,
        HaloRings = 1,
        MarquiseCutRings = 1,
        NatureInspiredRings = 1,
        NaturalRainbowRings = 1,
        Rings = 1
    }
}
