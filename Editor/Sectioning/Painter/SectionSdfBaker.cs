using Ameye.SRPUtilities.Enums;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Ameye.OutlinesToolkit.Editor.Sectioning.Painter
{
    public static class SectionSdfBaker
    {
        // Compute shader.
        private static ComputeShader _computeShader;
        
        // Kernels.
        private static int _seedKernel, _jfaKernel, _fillDistanceTransformKernel;
        
        // Temporary render textures.
        private static RenderTexture _tmp1, _tmp2;

        private static Material _sdfBakeMaterial;
        
        
        private const int PassInit = 0;
        private const int PassJump = 1;

        
        private static void InitializeTemporaryRenderTextures(Texture source)
        {
            if (_tmp1 != null && _tmp1.width == source.width && _tmp1.height == source.height) return;

            var desc = new RenderTextureDescriptor
            {
                width = source.width,
                height = source.height,
                volumeDepth = 1,
                msaaSamples = 1,
                graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                enableRandomWrite = true,
                dimension = TextureDimension.Tex2D,
                sRGB = false

            };
            
            _tmp1 = new RenderTexture(source.width, source.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _tmp1.enableRandomWrite = true;
            _tmp1.Create();
            
            _tmp2 = new RenderTexture(source.width, source.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _tmp2.enableRandomWrite = true;
            _tmp2.Create();

           /* _tmp1 = new RenderTexture(desc);
            _tmp1.Create();

            _tmp2 = new RenderTexture(desc);
            _tmp2.Create();*/
        }


        private static RenderTexture GetTemporaryRT(Texture source)
        {
            var tex = RenderTexture.GetTemporary(source.width, source.height,
                0,
                RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear);
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        public static Texture2D BakeSdfFromRT(RenderTexture source, Channel channel)
        {
            if (!TryInitializeSdfBakeMaterial()) {
                return null;
            }

            var tex0 = GetTemporaryRT(source);
            var tex1 = GetTemporaryRT(source);
            
            Graphics.Blit(source, tex0, _sdfBakeMaterial, PassInit);
            
            _sdfBakeMaterial.SetFloat("_Channel", (uint) channel);

            var steps = 32;

            var step = Mathf.RoundToInt(Mathf.Pow(steps - 1, 2));
            while (step != 0) {
                _sdfBakeMaterial.SetFloat("_Step", step);
                Graphics.Blit(tex0, tex1, _sdfBakeMaterial, PassJump);

                var tmp = tex0;
                tex0 = tex1;
                tex1 = tmp;

                step /= 2;
            }
            
            
            // Additional processing.
            var tempRT = GetTemporaryRT(source);//RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Graphics.Blit(tex0, tempRT);

            // read RenderTexture contents into a new Texture2D using ReadPixels
            var resultTexture = new Texture2D(tempRT.width, tempRT.height, TextureFormat.RGBAHalf, false, true);
            Graphics.SetRenderTarget(tempRT);
            resultTexture.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0, false);
            Graphics.SetRenderTarget(null);
            resultTexture.Apply();
            RenderTexture.ReleaseTemporary(tempRT);
            

            RenderTexture.ReleaseTemporary(tex0);
            RenderTexture.ReleaseTemporary(tex1);
            
            return resultTexture;
        }

       

        private static bool TryInitializeSdfBakeMaterial()
        {
            if (_sdfBakeMaterial != null) {
                return true;
            }

            var shader = Shader.Find("Hidden/BakeSDF");
            if (shader == null) {
                return false;
            }

            _sdfBakeMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "SDF Bake Material"
            };
            return true;
        }



        public static Texture2D GetSdfTextureFromRTCompute(RenderTexture sourceRT, Channel sourceChannel, int targetMask, float power)
        {
            InitializeTemporaryRenderTextures(sourceRT);
            
            // Initialize compute shader and kernels.
            _computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Packages/com.ameye.outlines-toolkit/Package Resources/Shaders/SectionSdf.compute");
            if (_computeShader == null) Debug.LogError("Compute shader 'Section Sdf' could not be found.");

            
            // initialize kernels
            _seedKernel = _computeShader.FindKernel("Seed");
            _jfaKernel = _computeShader.FindKernel("Flood");
            _fillDistanceTransformKernel = _computeShader.FindKernel("FillDistanceTransform");
            
            // compute thread group sizes
            //_computeShader.GetKernelThreadGroupSizes(_initSeedKernel, out var x, out var y, out var z);
            var threadGroups = new Vector3Int(
                Mathf.CeilToInt((float) sourceRT.width / 8.0f),
                Mathf.CeilToInt((float) sourceRT.height / 8.0f),
                1);

            // Seed.
            _computeShader.SetTexture(_seedKernel, "InputTexture", sourceRT);
            _computeShader.SetTexture(_seedKernel, "Source", _tmp1);
            _computeShader.SetInt("Width", sourceRT.width);
            _computeShader.SetInt("Height", sourceRT.height);
            _computeShader.SetFloat("Channel", (uint) sourceChannel);
            _computeShader.Dispatch(_seedKernel, threadGroups.x, threadGroups.y, threadGroups.z);
            
            // Flood.
            var totalSteps = (int) Mathf.Log(Mathf.Max(sourceRT.width, sourceRT.height), 2);
            for (var i = 0; i < totalSteps; i++)
            {
                var step = (int) Mathf.Pow(2, totalSteps - i - 1);
                _computeShader.SetInt("Step", step);
                
                _computeShader.SetTexture(_jfaKernel, "Source", _tmp1);
                _computeShader.SetTexture(_jfaKernel, "Result", _tmp2);
                _computeShader.Dispatch(_jfaKernel, threadGroups.x, threadGroups.y, threadGroups.z);
                Graphics.Blit(_tmp2, _tmp1);
            }
            
            // Compute SDF.
            _computeShader.SetTexture(_fillDistanceTransformKernel, "Source", _tmp1);
            _computeShader.SetTexture(_fillDistanceTransformKernel, "Result", _tmp2);
                _computeShader.Dispatch(_fillDistanceTransformKernel, threadGroups.x, threadGroups.y, threadGroups.z);
            
            
            // Additional processing.
            var tempRT = RenderTexture.GetTemporary(sourceRT.width, sourceRT.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Graphics.Blit(_tmp2, tempRT);

            // read RenderTexture contents into a new Texture2D using ReadPixels
            var resultTexture = new Texture2D(tempRT.width, tempRT.height, UnityEngine.TextureFormat.RGBAHalf, false, true);
            Graphics.SetRenderTarget(tempRT);
            resultTexture.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0, false);
            Graphics.SetRenderTarget(null);
            resultTexture.Apply();
            RenderTexture.ReleaseTemporary(tempRT);
            
            _tmp1.Release();
            _tmp2.Release();

            return resultTexture;
        }
    }
}