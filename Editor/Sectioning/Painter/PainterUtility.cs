using UnityEngine;

namespace Ameye.OutlinesToolkit.Editor.Sectioning.Painter
{
    public static class PainterUtility
    {
        public static readonly int MouseDataProperty = Shader.PropertyToID("_MouseInfo");
        public static readonly int BrushColorProperty = Shader.PropertyToID("_BrushColor");
        public static readonly int BrushTypeProperty = Shader.PropertyToID("_BrushType");
        public static readonly int BrushOpacityProperty = Shader.PropertyToID("_BrushOpacity");
        public static readonly int BrushSizeProperty = Shader.PropertyToID("_BrushSize");
        public static readonly int BrushHardnessProperty = Shader.PropertyToID("_BrushHardness");
        public static readonly int BaseMapProperty = Shader.PropertyToID("_BaseMap");
        public static readonly int SdfSectioningTextureProperty = Shader.PropertyToID("_SdfSectioningTexture");
        public static readonly int BlendOpProperty = Shader.PropertyToID("_BlendOp");
    }
}