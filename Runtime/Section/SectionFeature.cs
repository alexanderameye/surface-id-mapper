using System;
using Ameye.OutlinesToolkit.Section.Utilities;
using Ameye.SRPUtilities;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ameye.OutlinesToolkit.Section
{
    public class SectionFeature : ScriptableRendererFeature
    {
        public enum InjectionPoint
        {
            AfterRenderingTransparents = RenderPassEvent.AfterRenderingTransparents
        }

        public enum SectionBufferFormat
        {
            R16 = GraphicsFormat.R16_SFloat,
            R16G16B16A16 = GraphicsFormat.R16G16B16A16_SFloat,
            B10G11R11 = GraphicsFormat.B10G11R11_UFloatPack32
        }
        
        private class SectionPass : ScriptableRenderPass
        {
            private readonly Settings settings;
            
            // Render settings.
            private FilteringSettings filteringSettings;
            private RenderStateBlock renderStateBlock;
            
            private RTHandle sectionBufferColorTarget, sectionBufferDepthTarget;
            private readonly int sectionBufferShaderPropertyId = Shader.PropertyToID("_CameraSectioningTexture");
            
            public SectionPass(Settings settings)
            {
                // WARN: Be careful of creating/cloning materials here. You need to clean them up in Dispose().
                
                this.settings = settings;
                
                // Set up profilingSampler and renderPassEvent.
                profilingSampler = new ProfilingSampler(nameof(SectionPass));
                renderPassEvent = (RenderPassEvent) settings.injectionPoint;
                
                // Only render objects on some layers to the section buffer.
                filteringSettings = new FilteringSettings(RenderQueueRange.opaque, -1,
                    URPUtility.GetRenderingLayer(0));
                renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                
                
                // Initialize section buffer.
                sectionBufferColorTarget = RTHandles.Alloc(SectioningUtility.SectionBufferColorTargetId, SectioningUtility.SectionBufferColorTargetId);
            }

            // Called before executing the render pass.
            // Used to configure render targets and their clear state. Also to create temporary render target textures.
            // When empty this render pass will render to the active camera render target.
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                /*var colorDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                colorDescriptor.depthBufferBits = 16;
                colorDescriptor.msaaSamples = 1;
                colorDescriptor.graphicsFormat = (GraphicsFormat) settings.sectionBufferFormat;

                if (sectionBufferColorTarget.rt == null)
                {
                    cmd.GetTemporaryRT(sectionBufferShaderPropertyId, colorDescriptor, FilterMode.Point);
                }

                ConfigureTarget(sectionBufferColorTarget);
                ConfigureClear(ClearFlag.Color, settings.clearColor);*/
                
                // NOTE: ALTERNATIVE
                var colorDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                colorDescriptor.depthBufferBits = 0;
                colorDescriptor.graphicsFormat = (GraphicsFormat) settings.sectionBufferFormat;
                RenderingUtils.ReAllocateIfNeeded(ref sectionBufferColorTarget, colorDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: SectioningUtility.SectionBufferColorTargetId);
                ConfigureTarget(sectionBufferColorTarget,  renderingData.cameraData.renderer.cameraDepthTargetHandle);
                ConfigureClear(ClearFlag.Color, settings.clearColor); 
            }
            
            

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // Grab a command buffer.
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    // Enable section pass.
                    cmd.EnableKeyword(SectioningUtility.SectioningPassKeyword);
                    
                    // Execute the command buffer.
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    // Render to section buffer.
                    var sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                    var drawingSettings = CreateDrawingSettings(URPUtility.DefaultUrpShaderTags, ref renderingData, sortingCriteria);
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings,
                        ref renderStateBlock);
                    context.DrawSkybox(renderingData.cameraData.camera);
                    
                    // Set the section buffer.
                    cmd.SetGlobalTexture(SectioningUtility.SectionBufferShaderPropertyId, sectionBufferColorTarget);

                    // Disable section pass.
                    // NOTE: Moved to OnCameraCleanup
                    // cmd.DisableKeyword(SectioningUtility.SectioningPassKeyword);
                }

                // Execute the command buffer and release it.
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
            
            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.DisableKeyword(SectioningUtility.SectioningPassKeyword);
            }

            public void Dispose()
            {
                //cmd.ReleaseTemporaryRT(sectionBufferShaderPropertyId);
                sectionBufferColorTarget?.Release();;
            }
        }

        [Serializable]
        public class Settings
        {
            //[Range(0, 32)] public int layer = 2;
            public InjectionPoint injectionPoint = InjectionPoint.AfterRenderingTransparents;
            public SectionBufferFormat sectionBufferFormat = SectionBufferFormat.R16G16B16A16;
            public Color clearColor = Color.black;
            //public ClearFlag clearFlag = ClearFlag.All;
        }

        [SerializeField] private Settings settings = new();
        private SectionPass sectionPass;
        
        public override void Create()
        {
            // WARN: Be careful of creating/cloning materials here. You need to clean them up in Dispose().
            
            sectionPass = new SectionPass(settings);
        }

        // Injects one or multiple render passes in the renderer.
        // Gets called when setting up the renderer, once per-camera.
        // Gets called every frame, once per-camera.
        // Will not be called if the renderer feature is disabled in the renderer inspector.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Don't render in prefab isolation mode.
            if (PrefabStageUtility.GetCurrentPrefabStage()) return;
            
            // Don't render for the preview camera.
            if (renderingData.cameraData.isPreviewCamera) return;
            //if (renderingData.cameraData.camera != Camera.main) return;

            renderer.EnqueuePass(sectionPass);
        }

        // Release any resources that have been allocated.
        override protected void Dispose(bool disposing)
        {
            sectionPass.Dispose();
        }
    }
}