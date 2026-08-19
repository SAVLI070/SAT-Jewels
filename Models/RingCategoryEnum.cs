using System.ComponentModel.DataAnnotations;

namespace SAT1.Models
{
    public enum RingCategoryEnum : long
    {
        [Display(Name = "All Rings Collection")]
        Rings = 1,

        [Display(Name = "Anniversary Ring")]
        AnniversaryRings = 2,

        [Display(Name = "Rose Cut Ring")]
        RoseCutRings = 3,

        [Display(Name = "Antique Cut Ring")]
        AntiqueCutRings = 4,

        [Display(Name = "Engagement Ring")]
        EngagementRings = 5,

        [Display(Name = "Eternity Ring")]
        EternityRings = 6,

        [Display(Name = "Fancy Color Ring")]
        FancyColorRings = 7,

        [Display(Name = "Solitaire Ring")]
        SolitaireRings = 8,

        [Display(Name = "Three Stone Ring")]
        ToiEtMoiRings = 9,

        [Display(Name = "Halo Ring")]
        HaloRings = 10,

        [Display(Name = "Marquise Cut Ring")]
        MarquiseCutRings = 11,

        [Display(Name = "Nature Inspired Ring")]
        NatureInspiredRings = 12,

        [Display(Name = "Natural Rainbow Ring")]
        NaturalRainbowRings = 13
    }
}
