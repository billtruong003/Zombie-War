using System;

namespace BillInspector
{
    /// <summary>
    /// Color picker with a named palette. Palette can be defined via BillColorPaletteAsset.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillColorPaletteAttribute : BillDrawerAttribute
    {
        public string PaletteName { get; }

        public BillColorPaletteAttribute(string paletteName = null)
        {
            PaletteName = paletteName;
        }
    }
}
