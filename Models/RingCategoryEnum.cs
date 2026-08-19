using System.ComponentModel.DataAnnotations;

namespace SAT1.Models
{
    public enum RingCategoryEnum : long
    {
        [Display(Name = "Bridal Sets")]
        BridalSets = 2,

        [Display(Name = "Engagement Rings")]
        EngagementRings = 5,

        [Display(Name = "Wedding Rings")]
        WeddingRings = 6,

        [Display(Name = "Eternity Rings")]
        EternityRings = 6,

        [Display(Name = "Earrings")]
        Earrings = 14,

        [Display(Name = "Necklaces")]
        Necklaces = 15,

        [Display(Name = "Bracelets")]
        Bracelets = 17,

        [Display(Name = "Rings Collection")]
        Rings = 1,

        [Display(Name = "Anniversary Rings")]
        AnniversaryRings = 22,

        [Display(Name = "Rose Cut Rings")]
        RoseCutRings = 3,

        [Display(Name = "Antique Cut Rings")]
        AntiqueCutRings = 4,

        [Display(Name = "Fancy Color Rings")]
        FancyColorRings = 7,

        [Display(Name = "Solitaire Rings")]
        SolitaireRings = 8,

        [Display(Name = "Three Stone Rings")]
        ToiEtMoiRings = 9,

        [Display(Name = "Halo Rings")]
        HaloRings = 10,

        [Display(Name = "Marquise Cut Rings")]
        MarquiseCutRings = 11,

        [Display(Name = "Nature Inspired Rings")]
        NatureInspiredRings = 12,

        [Display(Name = "Natural Rainbow Rings")]
        NaturalRainbowRings = 13
    }
}
