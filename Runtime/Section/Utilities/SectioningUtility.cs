using UnityEngine;
using UnityEngine.Rendering;

namespace Ameye.OutlinesToolkit.Section.Utilities
{
    public static class SectioningUtility
    {
        public static readonly GlobalKeyword SectioningPassKeyword = GlobalKeyword.Create("_SECTIONING_PASS");

        public static readonly string SectionBufferColorTargetId = "_CameraSectioningTexture";
        public static readonly string SectionBufferDepthTargetId = "_SectionBufferDepth";
        public static readonly ShaderTagId SectioningTag = new("Sectioning");
        
        public static readonly int SectionBufferShaderPropertyId = Shader.PropertyToID("_CameraSectioningTexture");


    }
}