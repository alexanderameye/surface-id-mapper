using System;
using System.IO;
using Ameye.OutlinesToolkit.Editor.Sectioning.Enums;
using Ameye.OutlinesToolkit.Editor.Sectioning.Utilities;
using Ameye.SRPUtilities.Editor.DebugViewer;
using Ameye.TextureUtilities;
using Ameye.TextureUtilities.Editor.Utilities.SdfBaker;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Channel = Ameye.SRPUtilities.Enums.Channel;
using Object = UnityEngine.Object;
using TextureFormat = UnityEngine.TextureFormat;

namespace Ameye.OutlinesToolkit.Editor.Sectioning.Painter
{
    // TODO: READ https://docs.unity3d.com/ScriptReference/Texture2D.ReadPixels.html
    // TODO: READ https://docs.unity3d.com/ScriptReference/Graphics.CopyTexture.html

    public enum PaintMode
    {
        Additive,
        Subtractive
    }

    public enum PaintTarget
    {
        Paint,
        Mask
    }

    public static class SectionPainter
    {
        private static PaintMode _paintMode = PaintMode.Additive;

        private static bool _active;
        private static float _brushSize = 0.0f;

        private const int SectionSourceTextureResolution = 4096;
        private const int SectionSdfTextureResolution = 128;

        private static bool inputDisabled;

        // Mesh intersection.
        private static Ray _intersectionRay;
        private static MeshIntersection _meshIntersection;
        private static Vector2 _dragStartPosition;
        private static Material _paintMat, _dilationMat;

        // Painting.
        private static Vector4 _mouseData = Vector4.zero;
        public static RenderTexture _sourcePaintRT;
        private static RenderTexture _dilatedPaintRT, _paintedRT;
        private const float BrushRadiusInterval = 0.005f;
        private static BrushType _brushType = BrushType.Sphere;

        // Selection.
        public static GameObject SelectedGameObject { get; private set; }
        private static MeshRenderer _selectedMeshRenderer;
        private static Matrix4x4 _selectedGameObjectTransformationMatrix;
        private static MeshFilter _selectedMeshFilter;
        private static Material _selectedMeshMaterial;
        private static int[] _selectedMeshRendererInstanceIds;
        private static Mesh _mesh;
        private static bool _dilate = true;
        private static bool _painting;

        // Section texture.
        private static Texture _assignedTexture;
        private static string _sectionTexturePath, _sectionSourceTexturePath;

        // Actions.
        public static event Action<bool> ActiveStatusChanged = delegate { };


        public static bool IsActive()
        {
            return _active;
        }

        public static void Enter(Material material, string materialGuid, string sectionTexturePath, string sectionSourceTexturePath, Texture2D sectionSourceTexture = null)
        {
            // Activate the tool.
            _active = true;
            ActiveStatusChanged(true);

            // Hide tools (fixme: idk what this does really, nothing it seems?).
            Tools.hidden = true;

            // Enable section painter debug view.
            DebugViewHandler.EnableDebugView(true);
            DebugViewHandler.SetDebugView(DebugViewHandler.DebugViewsAsset.debugViews.Find(view => view.name == "Section Painter"));

            // Disable all graphics settings for scene view.
            SceneView.lastActiveSceneView.sceneViewState.SetAllEnabled(false);
            SceneView.RepaintAll();

            // Focus camera on the selected gameobject.
            SceneView.lastActiveSceneView.FrameSelected();

            // Register information about the selected gameobject.
            SelectedGameObject = Selection.activeGameObject;
            _selectedMeshRenderer = SelectedGameObject.GetComponent<MeshRenderer>();
            _selectedMeshMaterial = _selectedMeshRenderer.sharedMaterial;
            _selectedMeshFilter = SelectedGameObject.GetComponent<MeshFilter>();
            _selectedMeshRendererInstanceIds = new[] {_selectedMeshRenderer.GetInstanceID()};
            _mesh = _selectedMeshFilter.sharedMesh;
            _selectedGameObjectTransformationMatrix = SelectedGameObject.transform.localToWorldMatrix;

            // The section texture paths.
            _sectionTexturePath = sectionTexturePath;
            _sectionSourceTexturePath = sectionSourceTexturePath;

            // If a sectionSourceTexture already exists then copy sectionSourceTexture into _sourcePaintRT.
            if (sectionSourceTexture != null)
            {
                // TODO: Does blit work here?
                _sourcePaintRT = new RenderTexture(sectionSourceTexture.width, sectionSourceTexture.height, 0)
                {
                    name = "Source Paint",
                    enableRandomWrite = true, // Required for compute usage.
                    filterMode = FilterMode.Point, // TODO: Best?
                    graphicsFormat = GraphicsFormat.R32G32_SFloat // TODO: Less bits?
                };
                _sourcePaintRT.Create();
                RenderTexture.active = _sourcePaintRT;
                Graphics.Blit(sectionSourceTexture, _sourcePaintRT);
            }

            // If a sectionSourceTexture does not exist yet then create it from scratch using a chosen width/height.
            else
            {
                Debug.Log("No section source was given, creating new one.");

                _sourcePaintRT = new RenderTexture(SectionSourceTextureResolution, SectionSourceTextureResolution, 0)
                {
                    name = "Source Paint",
                    enableRandomWrite = true, // Required for compute usage.
                    filterMode = FilterMode.Point, // TODO: Best?
                    graphicsFormat = GraphicsFormat.R32G32_SFloat // TODO: Less bits?
                };
                _sourcePaintRT.Create();
            }

            //   Shader.SetGlobalTexture("_SdfSectioningSourceTexture", _sourcePaintRT);

            // Set texture
            // FIXME: This sets the undilated texture which is not a big deal but not ideal.. sicne you want to get a good accurate preview
            _assignedTexture = _selectedMeshMaterial.GetTexture(PainterUtility.SdfSectioningTextureProperty);
            _selectedMeshMaterial.SetTexture(PainterUtility.SdfSectioningTextureProperty, _sourcePaintRT);

            // Initialize other render textures.
            _dilatedPaintRT = new RenderTexture(SectionSourceTextureResolution, SectionSourceTextureResolution, 0)
            {
                name = "Dilated Paint",
                anisoLevel = 0,
                useMipMap = false,
                filterMode = FilterMode.Point, // TODO: Best?
                enableRandomWrite = true, // Required for compute usage.
                graphicsFormat = GraphicsFormat.R32G32_SFloat // TODO: Less bits?
            };
            _dilatedPaintRT.Create();

            /*_paintedRT = new RenderTexture(resolution, resolution, 0)
            {
                name = "Painted RT",
                anisoLevel = 0,
                useMipMap = false,
                filterMode = FilterMode.Point, // TODO: Best?
                enableRandomWrite = true, // Required for compute usage.
                graphicsFormat = GraphicsFormat.R32G32_SFloat // TODO: Less bits?
            };
            _paintedRT.Create();*/

            // Set up paint and dilate materials.
            _paintMat = new Material(Shader.Find("Hidden/Paint"));
            _dilationMat = new Material(Shader.Find("Hidden/Dilate"));
            _paintMat.SetVector(PainterUtility.BrushColorProperty, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            _paintMat.SetFloat(PainterUtility.BrushOpacityProperty, 1.0f);
            _paintMat.SetFloat(PainterUtility.BrushSizeProperty, 0.05f);
            _brushSize = 0.05f;
            _paintMat.SetFloat(PainterUtility.BrushHardnessProperty, 1.0f);
            _paintMat.SetInt(PainterUtility.BlendOpProperty, (int) BlendOp.Add);
            _paintMat.SetTexture(PainterUtility.BaseMapProperty, _sourcePaintRT);
            _paintMat.SetVector(PainterUtility.MouseDataProperty, Vector4.zero);
            _paintMat.SetInteger(PainterUtility.BrushTypeProperty, (int) _brushType);

            //ClearAll();


            // Events.
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            SceneView.duringSceneGui += OnDuringSceneGui; // important: this is done AFTER _selectedGameObject has been set
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            Undo.undoRedoPerformed += OnUndoRedo;


            // show scene view overlay
            SectionPainterOverlay.Show();
        }

        public static void Leave()
        {
            // Clean up materials.
            Object.DestroyImmediate(_paintMat);
            Object.DestroyImmediate(_dilationMat);

            // scene view
            SceneView.lastActiveSceneView.sceneViewState.SetAllEnabled(true);
            SceneView.RepaintAll();

            // ui
            Tools.hidden = false; // fixme: idk what this does really, nothing it seems?
            SectionPainterOverlay.Hide(); // hide scene view overlay
            DebugViewHandler.EnableDebugView(false); // disable sectioning debug view

            _active = false;
            ActiveStatusChanged(false);

            // reset the selected gameobject
            Selection.activeGameObject = SelectedGameObject;

            // Save _sourcePaintRT as the updated section source texture.
            var source = SaveSectionSourceTexture(_sourcePaintRT, _sectionSourceTexturePath);

            // Process the _sourcePaintRT so it gets an SDF and dilation pass.
            ProcessAndSaveSdfSectionTexture(source, _sectionTexturePath);

            // Cleanup.
            //_paintedRT.Release();
            _sourcePaintRT.Release();
            _dilatedPaintRT.Release();

            // Restore old used texture.
            _selectedMeshMaterial.SetTexture("_SdfSectioningTexture", _assignedTexture);


            #region events

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            SceneView.duringSceneGui -= OnDuringSceneGui;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            Undo.undoRedoPerformed -= OnUndoRedo;

            #endregion

        }

        private static void BlitWithShader(RenderTexture source, RenderTexture target, Shader shader, Action<Material> setMaterialProperties, int pass = 0)
        {
            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            setMaterialProperties(material);
            Graphics.Blit(source, target, material, pass);
            Object.DestroyImmediate(material);
        }

        private static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            source.Apply();

            var rt = new RenderTexture(width, height, 24);
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);
            var result = new Texture2D(width, height);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();
            return result;
        }

        

        private static void ProcessAndSaveSdfSectionTexture(Texture source, string sdfSectionTexturePath)
        {
            // Bake an SDF from the R channel of the source into the R channel of a target texture.
            var sdfTextureR = SdfBaker.BakeSDF(source, TextureUtilities.Channel.R, TextureUtilities.Channel.R, 0.04f, 1.0f);
            
            // Bake an SDF from the G channel of the source into the G channel of a target texture..
            var sdfTextureG = SdfBaker.BakeSDF(source, TextureUtilities.Channel.G, TextureUtilities.Channel.G, 0.04f, 1.0f);
            
     
            // Combine the SDFs from the R and G channel.
            var descriptor = new RenderTextureDescriptor(source.width, source.height, GraphicsFormat.R16G16B16A16_UNorm, 0, 0);
            descriptor.sRGB = false;
            var tempRT = RenderTexture.GetTemporary(descriptor);

           // var tempRT = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
           BlitWithShader(null, tempRT, Shader.Find("Section Painter/Combine Section Textures"), material =>
            {
                material.SetTexture("_TexR", sdfTextureR);
                material.SetTexture("_TexG", sdfTextureG);
            });

            // read RenderTexture contents into a new Texture2D using ReadPixels
            var result = new Texture2D(source.width, source.height, GraphicsFormat.R8G8B8A8_UNorm, 0, TextureCreationFlags.None);
            var activeRT = RenderTexture.active;
            RenderTexture.active = tempRT;
            result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            RenderTexture.active = activeRT;
            
           
            
            RenderTexture.ReleaseTemporary(tempRT);
            
            RenderTexture.ReleaseTemporary(sdfTextureR);
            RenderTexture.ReleaseTemporary(sdfTextureG);
            
            
            
     
            // Downscale texture.
            var scaledDownTexture = ResizeTexture(result, SectionSdfTextureResolution, SectionSdfTextureResolution);
            
            // Save texture.
           // File.WriteAllBytes(sdfSectionTexturePath, sdfTextureR.EncodeToPNG());
            File.WriteAllBytes(sdfSectionTexturePath, scaledDownTexture.EncodeToPNG());
            AssetDatabase.Refresh();
            Object.DestroyImmediate(scaledDownTexture);
            
            // Set import settings.    
            var relativeIndex = sdfSectionTexturePath.IndexOf("Assets/", StringComparison.Ordinal);
            if (relativeIndex < 0) return;
            sdfSectionTexturePath = sdfSectionTexturePath[relativeIndex..];
            if (AssetImporter.GetAtPath(sdfSectionTexturePath) is TextureImporter importer)
            {
                importer.sRGBTexture = false;
                var settings = importer.GetDefaultPlatformTextureSettings();
                settings.format = TextureImporterFormat.RGB24;
                importer.SetPlatformTextureSettings(settings);
                importer.SaveAndReimport();
                

                /*importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Trilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = true;*/
            }
            
            //AssetDatabase.ImportAsset(sdfSectionTexturePath);
            Debug.Log("Saved processed sectioning texture: " + sdfSectionTexturePath);
            
            //Object.DestroyImmediate(source);
        }

        private static Texture2D SaveSectionSourceTexture(RenderTexture source, string path)
        {
            // TODO: Set other import settings or change graphics format or something?
            
            var result = new Texture2D(source.width, source.height, GraphicsFormat.R8G8B8A8_UNorm, 0, TextureCreationFlags.None);
            
            // Switch active render target and read pixels.
            var activeRT = RenderTexture.active;
            RenderTexture.active = source;
            result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            RenderTexture.active = activeRT;
            
            
            // Set B channel to 0 why is this needed? FIXME: Does RG32 format not work?
            var pixels = result.GetPixels();
            for (var p = 0; p < pixels.Length; p++)
            {
                pixels[p].b = 0;
            }
            result.SetPixels(pixels);
            result.Apply();
            
            // Save texture.
            File.WriteAllBytes(path, result.EncodeToPNG());
            AssetDatabase.Refresh();
            
            // Clean up created temporary texture.
            //Object.DestroyImmediate(result);
            
            // Set importer settings.
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return null;
            importer.sRGBTexture = false;
            var settings = importer.GetDefaultPlatformTextureSettings();
            settings.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
            
            Debug.Log("Saved SDF source to " + path);
            return result;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // TODO: Check this https://github.com/asaia/BleedingEdgeEffects_GDC_2020/blob/master/Assets/GDC_Demos/UVSpaceTransformation/Scripts/UVSpaceRenderer.cs and do similarly

            if (_paintMat == null || _dilationMat == null) return;
            if (!_painting) return;
            if (!_meshIntersection.found) return;

            // Set shader parameters.
            _paintMat.SetFloat(PainterUtility.BrushOpacityProperty, 1.0f);
            _paintMat.SetFloat(PainterUtility.BrushSizeProperty, _brushSize);
            _paintMat.SetFloat(PainterUtility.BrushHardnessProperty, 1.0f);
            _paintMat.SetVector(PainterUtility.MouseDataProperty, _meshIntersection.position);

            switch (_paintMode)
            {
                case PaintMode.Additive:
                    _paintMat.SetInt(PainterUtility.BlendOpProperty, (int) BlendOp.Max);
                    break;
                case PaintMode.Subtractive:
                    _paintMat.SetInt(PainterUtility.BlendOpProperty, (int) BlendOp.ReverseSubtract);
                    break;
            }

            // Set up command buffer.
            var cmd = new CommandBuffer();
            cmd.name = "Render Paint Mask";



           // Set paint mask as render target.
                cmd.SetRenderTarget(_sourcePaintRT);
                // cmd.ClearRenderTarget(true, true, Color.black); // Do not do this since you want to accumulate the paint.

                // Render mesh into paint mask.
                //cmd.DrawRenderer(_selectedMeshRenderer, _paintMat);
                cmd.DrawMesh(_mesh, SelectedGameObject.transform.localToWorldMatrix, _paintMat);
            

            cmd.SetRenderTarget(_dilatedPaintRT);
            if (_dilate)
            {
                cmd.Blit(_sourcePaintRT, _dilatedPaintRT, _dilationMat);
            }
            else
            {
                // NOTE: NOT DILATED HERE BUT JUST USED AS THE "FINAL" MASK
                cmd.Blit(_sourcePaintRT, _dilatedPaintRT);
            }

            // TODO: These steps are not really needed? Just to show on mesh? Kinda weird
            // cmd.Blit(_processedPaintRT, _paintedRT);

            // Set the sdf sectioning texture.
            _selectedMeshMaterial.SetTexture(PainterUtility.SdfSectioningTextureProperty, _dilatedPaintRT);

            context.ExecuteCommandBuffer(cmd);
            cmd.Release();
            context.Submit();
        }

        private static void OnUndoRedo()
        {

        }

        private static void OnProjectChanged()
        {
            // Leave section painter.
            // Leave();
            // TODO: Re-enable this? But avoid LEAVE BEING CALLED TWICE!!!!!
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            // Leave section painter.
            Leave();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            // Leave section painter.
            Leave();
        }



        private static void OnDuringSceneGui(SceneView sceneView)
        {
            var currentEvent = Event.current;
            var mousePosition = currentEvent.mousePosition;

            // Prevent selection of gameobjects while editing.
            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (currentEvent.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);

            // Get event type.
            var eventType = currentEvent.GetTypeForControl(controlId);

            // scene view drawing (while alt/option key is possible pressed)
            if (currentEvent.type == EventType.Repaint)
            {
                Handles.DrawOutline(_selectedMeshRendererInstanceIds, Color.white);
            }

            // Prevent scene-view panning with right-click.
            PreventButtonInput(currentEvent, EventType.MouseDown, 1);

            // Don't do anything while holding alt/option key (is used for scene camera movement).
            if (currentEvent.alt || currentEvent.button == 2) return;

            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
                SceneView.RepaintAll();


            // Handle event types.
            switch (eventType)
            {
                // Key pressed.
                case EventType.KeyDown:
                    switch (currentEvent.keyCode)
                    {
                        case KeyCode.Escape:
                            Event.current.Use();
                            Leave();
                            break;
                        case KeyCode.Space:
                            SwitchBrushType();
                            break;
                        case KeyCode.C:
                            ClearSectionPaint();
                            break;
                    }
                    break;
                case EventType.ScrollWheel:
                    if (PreventCustomUserHotkey(EventType.ScrollWheel, EventModifiers.Shift, KeyCode.None))
                    {
                        if (currentEvent.delta.x < 0)
                        {
                            _brushSize += BrushRadiusInterval;
                            SceneView.RepaintAll();
                        }
                        else if (currentEvent.delta.x > 0)
                        {
                            _brushSize -= BrushRadiusInterval;
                            _brushSize = Mathf.Max(BrushRadiusInterval, _brushSize);
                            SceneView.RepaintAll();
                        }
                    }
                    break;
                case EventType.MouseDrag:
                    _painting = true;

                    // Switch paint mode based on whether left or right button is clicked.
                    _paintMode = currentEvent.button == 0 ? PaintMode.Additive : PaintMode.Subtractive;
                    switch (_paintMode)
                    {
                        case PaintMode.Additive:
                            _paintMat.SetInt(PainterUtility.BlendOpProperty, (int) BlendOp.Max);
                            break;
                        case PaintMode.Subtractive:
                            _paintMat.SetInt(PainterUtility.BlendOpProperty, (int) BlendOp.ReverseSubtract);
                            break;
                    }

                    _paintMat.SetVector(PainterUtility.BrushColorProperty, currentEvent.shift ? Color.green : Color.red);

                    // Whenever the mouse has moved above a certain threshold distance.
                    if ((mousePosition - _dragStartPosition).magnitude > 0f)
                    {
                        // Perform intersection test.
                        _intersectionRay = HandleUtility.GUIPointToWorldRay(mousePosition);
                        if (_meshIntersection.Raycast(_intersectionRay, _selectedMeshFilter))
                        {
                            _mouseData = _meshIntersection.position;
                            _painting = true;

                            // Tell the paint shader that a mouse button is being clicked.
                            // _mouseData.w = 1;
                            // _paintMat.SetVector(PainterUtility.MouseDataProperty, _mouseData);


                            // Tell the paint shader whether the left button or the right button is being pressed to either paint or erase.
                            // var blendop = currentEvent.button == 0 ? (int) BlendOp.Add : (int) BlendOp.ReverseSubtract;
                            // _paintMat.SetInt(PainterUtility.BlendOpProperty, blendop);
                            if (currentEvent.button == 0) 
                            {
                                _paintMat.SetFloat(PainterUtility.BrushSizeProperty, _brushSize);
                                //_paintMat.SetInt(PainterUtility.BlendOpProperty, (int) BlendOp.Add);
                            }
                            else
                            {
                                // NOTE: We set the radius when erasing a bit higher to be more forgiving.
                                _paintMat.SetFloat(PainterUtility.BrushSizeProperty, _brushSize * 1.5f);
                                // _paintMat.SetInt(PainterUtility.BlendOpProperty, (int) BlendOp.ReverseSubtract);
                            }
                        }
                    }
                    break;
                // Mouse move.
                case EventType.MouseMove:
                    
                    // Perform raycast to update the paint cursor.
                    _intersectionRay = HandleUtility.GUIPointToWorldRay(mousePosition);
                    _meshIntersection.Raycast(_intersectionRay, _selectedMeshFilter);

                    // Tell the paint shader that the mouse button is not being pressed.
                    //_mouseData = Vector3.positiveInfinity;
                    _mouseData.w = 0;
                    _painting = false;
                    // _paintMat.SetVector(PainterUtility.MouseDataProperty, _mouseData);

                    // Repaint to update position of paint cursor.
                    SceneView.RepaintAll();
                    break;
                // Scene view drawing (while alt/option key is not pressed).
                case EventType.Repaint:
                    OnSceneViewRepaint(sceneView);
                    break;
            }
        }

        private static void SwitchBrushType()
        {
            _brushType = _brushType switch
            {
                BrushType.Sphere => BrushType.Square,
                BrushType.Square => BrushType.Sphere,
                _ => _brushType
            };
            
            _paintMat.SetInteger(PainterUtility.BrushTypeProperty, (int)_brushType);
        }

        private static void UpdatePaintTexture()
        {
            _painting = true;
            // FIXME: Force re-rerender not working...
        }

        public static bool PreventButtonInput(Event currentEvent, EventType type, int button)
        {
            if (currentEvent.type != type || currentEvent.button != button) return false;
            currentEvent.Use();
            return true;
        }

        public static bool PreventCustomUserHotkey(EventType type, EventModifiers codeModifier, KeyCode hotkey)
        {
            var currentEvent = Event.current;
            if (currentEvent.type == type && currentEvent.modifiers == codeModifier && currentEvent.keyCode == hotkey)
            {
                currentEvent.Use();
                return true;
            }

            return false;
        }

        private static void OnSceneViewRepaint(SceneView sceneView)
        {
            if (!_meshIntersection.found) return;

            // Get hit information.
            var hitPoint = _meshIntersection.position;
            var hitNormal = _meshIntersection.normal;
            
            var indicatorSize = _brushSize * 1.3f;

            // Draw mouse indicator.
            // TODO: Take brush radius into account correctly and not like this... .
            
            switch (_brushType)
            {
                // Draw square brush indicator.
                case BrushType.Square:
                    
                    
                    // Calculate the orientation matrix for the brush handle.
                    var upVector = Vector3.up;
                    var tangent = Vector3.Cross(upVector, hitNormal).normalized;
                    var bitangent = Vector3.Cross(hitNormal, tangent).normalized;
                    var orientationMatrix = Matrix4x4.TRS(hitPoint, Quaternion.LookRotation(hitNormal, bitangent), Vector3.one);

                    // Draw mouse indicator.
                    var brushVerts = new Vector3[4];
                    brushVerts[0] = new Vector3(-indicatorSize, -indicatorSize, 0);
                    brushVerts[1] = new Vector3(-indicatorSize, indicatorSize, 0);
                    brushVerts[2] = new Vector3(indicatorSize, indicatorSize, 0);
                    brushVerts[3] = new Vector3(indicatorSize, -indicatorSize, 0);
                    
                    var brushVertsSmall = new Vector3[4];
                    var smallBrushSize = indicatorSize * 0.9f;
                    brushVertsSmall[0] = new Vector3(-smallBrushSize, -smallBrushSize, 0);
                    brushVertsSmall[1] = new Vector3(-smallBrushSize, smallBrushSize, 0);
                    brushVertsSmall[2] = new Vector3(smallBrushSize, smallBrushSize, 0);
                    brushVertsSmall[3] = new Vector3(smallBrushSize, -smallBrushSize, 0);

                    Handles.matrix = orientationMatrix;
                    Handles.DrawSolidRectangleWithOutline(brushVerts, new Color(0.0f, 0.0f, 0.0f, 0.9f), new Color(0.0f, 0.0f, 0.0f, 0.9f));
                    Handles.DrawSolidRectangleWithOutline(brushVertsSmall, new Color(1.0f, 1.0f, 1.0f, 0.3f),new Color(1.0f, 1.0f, 1.0f, 0.3f) );

                    // Reset the matrix to the identity matrix.
                    Handles.matrix = Matrix4x4.identity;
                    break;
                // Draw sphere brush indicator.
                case BrushType.Sphere:
                    Handles.color = new Color(0.0f, 0.0f, 0.0f, 0.9f);
                    Handles.DrawSolidDisc(hitPoint, hitNormal, indicatorSize);
                    Handles.color = new Color(1.0f, 1.0f, 1.0f, 0.3f);
                    Handles.DrawSolidDisc(hitPoint, hitNormal, indicatorSize * 0.9f);
                    break;
            }


            //Handles.color = new Color(0.0f, 1.0f, 0.0f, 1.0f);
            //Handles.DrawLine(hitPoint, hitPoint + hitNormal * handleSize * 0.5f, 2.0f);

            SceneView.RepaintAll();
        }

        public static void SetBrushColor(Color color)
        {
            _paintMat.SetColor(PainterUtility.BrushColorProperty, color);
        }

        public static void SetBrushRadius(float radius)
        {
            _paintMat.SetFloat(PainterUtility.BrushSizeProperty, radius);
            _brushSize = radius;
        }

        public static void SetBrushHardness(float hardness)
        {
            _paintMat.SetFloat(PainterUtility.BrushHardnessProperty, hardness);
        }

        public static void SetBrushOpacity(float opacity)
        {
            _paintMat.SetFloat(PainterUtility.BrushOpacityProperty, opacity);
        }

        public static void SetDilation(bool dilate)
        {
            _dilate = dilate;
        }

        public static void ClearSectionPaint()
        {
            if(EditorUtility.DisplayDialog("Clear section paint data?",
                   "Are you sure you want to clear the section paint data for " + SelectedGameObject.name
                                                                                + "?", "Clear", "Abort"))
            {
                var rt = RenderTexture.active;
                RenderTexture.active = _sourcePaintRT;
                GL.Clear(true, true, Color.clear);
                RenderTexture.active = rt;
                UpdatePaintTexture();
            }
        }

        public static void ClearPaint()
        {
            // TODO: Clear R channel
        }

        public static void ClearMask()
        {
            // TODO: Clear G channel
        }

        public static void SetTargetChannel(string channel)
        {
            // TODO: Make this more robust.
            switch (channel)
            {
                case "Paint (R)":
                    _paintMat.SetVector(PainterUtility.BrushColorProperty, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
                    break;
                case "Mask (G)":
                    _paintMat.SetVector(PainterUtility.BrushColorProperty, new Vector4(0.0f, 1.0f, 0.0f, 1.0f));
                    break;
            }
        }

    }
}