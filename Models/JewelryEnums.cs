using System;
using System.ComponentModel.DataAnnotations;

namespace SAT1.Models
{
    // =========================================================================
    // LUXURY JEWELRY INDUSTRY ENUMS (GIA & FINE JEWELRY STANDARDS)
    // =========================================================================

    public enum MetalTypeEnum : int
    {
        [Display(Name = "14K Yellow Gold")]
        YellowGold14K = 1,

        [Display(Name = "18K Yellow Gold")]
        YellowGold18K = 2,

        [Display(Name = "14K White Gold")]
        WhiteGold14K = 3,

        [Display(Name = "18K White Gold")]
        WhiteGold18K = 4,

        [Display(Name = "14K Rose Gold")]
        RoseGold14K = 5,

        [Display(Name = "18K Rose Gold")]
        RoseGold18K = 6,

        [Display(Name = "Platinum 950")]
        Platinum950 = 7,

        [Display(Name = "Platinum 900")]
        Platinum900 = 8,

        [Display(Name = "10K Yellow Gold")]
        YellowGold10K = 9,

        [Display(Name = "10K White Gold")]
        WhiteGold10K = 10,

        [Display(Name = "10K Rose Gold")]
        RoseGold10K = 11,

        [Display(Name = "Sterling Silver 925")]
        SterlingSilver925 = 12
    }

    public enum DiamondClarityEnum : int
    {
        [Display(Name = "FL")]
        FL = 1,     // Flawless

        [Display(Name = "IF")]
        IF = 2,     // Internally Flawless

        [Display(Name = "VVS1")]
        VVS1 = 3,   // Very Very Slightly Included 1

        [Display(Name = "VVS2")]
        VVS2 = 4,   // Very Very Slightly Included 2

        [Display(Name = "VS1")]
        VS1 = 5,    // Very Slightly Included 1

        [Display(Name = "VS2")]
        VS2 = 6,    // Very Slightly Included 2

        [Display(Name = "SI1")]
        SI1 = 7,    // Slightly Included 1

        [Display(Name = "SI2")]
        SI2 = 8,    // Slightly Included 2

        [Display(Name = "I1")]
        I1 = 9      // Included 1
    }

    public enum DiamondColorEnum : int
    {
        [Display(Name = "D")]
        ColorD = 1,   // Exceptional Colorless

        [Display(Name = "E")]
        ColorE = 2,   // Colorless

        [Display(Name = "F")]
        ColorF = 3,   // Colorless

        [Display(Name = "G")]
        ColorG = 4,   // Near Colorless

        [Display(Name = "H")]
        ColorH = 5,   // Near Colorless

        [Display(Name = "I")]
        ColorI = 6,   // Near Colorless

        [Display(Name = "J")]
        ColorJ = 7,   // Near Colorless

        [Display(Name = "Fancy Yellow")]
        FancyYellow = 8,

        [Display(Name = "Fancy Pink")]
        FancyPink = 9,

        [Display(Name = "Fancy Blue")]
        FancyBlue = 10,

        [Display(Name = "Fancy Green")]
        FancyGreen = 11
    }

    public enum DiamondShapeEnum : int
    {
        [Display(Name = "Round Brilliant")]
        RoundBrilliant = 1,

        [Display(Name = "Princess Cut")]
        PrincessCut = 2,

        [Display(Name = "Cushion Cut")]
        CushionCut = 3,

        [Display(Name = "Oval Cut")]
        OvalCut = 4,

        [Display(Name = "Emerald Cut")]
        EmeraldCut = 5,

        [Display(Name = "Radiant Cut")]
        RadiantCut = 6,

        [Display(Name = "Pear Cut")]
        PearCut = 7,

        [Display(Name = "Marquise Cut")]
        MarquiseCut = 8,

        [Display(Name = "Heart Cut")]
        HeartCut = 9,

        [Display(Name = "Asscher Cut")]
        AsscherCut = 10,

        [Display(Name = "Rose Cut")]
        RoseCut = 11,

        [Display(Name = "Old Mine Cut")]
        OldMineCut = 12
    }

    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum enumValue)
        {
            var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
            if (fieldInfo == null) return enumValue.ToString();

            var attributes = (DisplayAttribute[])fieldInfo.GetCustomAttributes(typeof(DisplayAttribute), false);
            return attributes.Length > 0 ? attributes[0].Name ?? enumValue.ToString() : enumValue.ToString();
        }
    }
}
