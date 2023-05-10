using System.IO;
using Ameye.OutlinesToolkit.Editor.Sectioning.Marker;
using Ameye.OutlinesToolkit.Editor.Sectioning.Painter;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ameye.OutlinesToolkit.Editor.Sectioning
{
    [Icon("d_SceneAsset Icon")]
    [Overlay(typeof(SceneView), OverlayID, "Section Editor")]
    public class SectionEditorToolbarOverlay : ToolbarOverlay, ITransientOverlay
    {
        public const string OverlayID = "section-editor-overlay";

        private SectionEditorToolbarOverlay() : base(
            SectionMarkerToggle.ID,
            SectionPainterToggle.ID,
            SectionEditorContextLabel.ID
        )
        {
        }

        public bool visible { get; private set; }

        override protected Layout supportedLayouts => Layout.HorizontalToolbar | Layout.VerticalToolbar;
        
        public override void OnCreated()
        {
            SceneView.duringSceneGui += OnDuringSceneGui;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged += OnSelectionChanged;
            visible = IsSelectionValid();
        }

        public override void OnWillBeDestroyed()
        {
            SceneView.duringSceneGui -= OnDuringSceneGui;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= OnSelectionChanged;
        }

        private void OnDuringSceneGui(SceneView sceneView)
        {
            // if the section painter is not meant to be used, do nothing
            if (!visible) return;

            var currentEvent = Event.current;

           /* switch (currentEvent.type)
            {
                case EventType.KeyDown:
                    if (currentEvent.keyCode == KeyCode.Tab && !SectionMarker.IsActive())
                    {
                        SectionMarker.Enter();
                    }
                    break;
            }*/
        }

        private void OnSelectionChanged()
        {
            visible = IsSelectionValid();
        }

        private static bool IsSelectionValid()
        {
            // TODO: Optimize with less GetComponent calls.
            return SectionMarker.IsActive() ||
                   Selection.activeObject != null &&
                   Selection.activeGameObject != null &&
                   Selection.activeGameObject.activeSelf &&
                   Selection.activeGameObject.GetComponent<MeshRenderer>() != null && // required for vertex painting
                   Selection.activeGameObject.GetComponent<MeshRenderer>().enabled && // required for vertex painting
                   Selection.activeGameObject.GetComponent<MeshFilter>() != null; // required for intersection test
        }
    }

    [EditorToolbarElement(ID, typeof(SceneView))]
    public class SectionMarkerToggle : EditorToolbarToggle
    {
        public const string ID = SectionEditorToolbarOverlay.OverlayID + "/section-marker-toggle";

        private const string Tooltip = "Toggle section marker.";

        public SectionMarkerToggle()
        {
            var content = EditorGUIUtility.TrTextContentWithIcon("", Tooltip, "d_FilterByType@2x");
            text = content.text;
            tooltip = content.tooltip;
            icon = content.image as Texture2D;

            this.RegisterValueChangedCallback(Toggle);

            OnSelectionChanged();

            // keep track of panel events
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        protected virtual void OnAttachToPanel(AttachToPanelEvent evt)
        {
            SectionMarker.ActiveStatusChanged += OnSectionMarkerActiveStatusChanged;
            SectionPainter.ActiveStatusChanged += OnSectionPainterActiveStatusChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnSectionMarkerActiveStatusChanged(bool active)
        {
            SetValueWithoutNotify(active);
        }

        private void OnSectionPainterActiveStatusChanged(bool active)
        {
            SetEnabled(!active);
        }

        protected virtual void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            SectionMarker.ActiveStatusChanged -= OnSectionMarkerActiveStatusChanged;
            SectionPainter.ActiveStatusChanged -= OnSectionPainterActiveStatusChanged;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (SectionMarker.IsActive()) SectionMarker.Leave();
            if (SelectionValid()) SetEnabled(true);
            else SetEnabled(false);
        }

        private bool SelectionValid()
        {
            // TODO: Optimize with less GetComponent calls.
            return Selection.activeObject != null &&
                   Selection.activeGameObject != null &&
                   Selection.activeGameObject.activeSelf &&
                   Selection.activeGameObject.GetComponent<MeshRenderer>() != null && // required for vertex painting
                   Selection.activeGameObject.GetComponent<MeshRenderer>().enabled && // required for vertex painting
                   Selection.activeGameObject.GetComponent<MeshFilter>() != null;// && // required for intersection test
                   Selection.activeGameObject.GetComponent<MeshRenderer>().sharedMaterial.HasProperty("_SdfSectioningTexture"); // required for authoring sdf texture

        }


        private void Toggle(ChangeEvent<bool> evt)
        {
            if (evt.newValue) SectionMarker.Enter();
            else SectionMarker.Leave();
        }
    }

    [EditorToolbarElement(ID, typeof(SceneView))]
    public class SectionPainterToggle : EditorToolbarToggle
    {
        public const string ID = SectionEditorToolbarOverlay.OverlayID + "/section-painter-toggle";

        private const string Tooltip = "Toggle section painter.";

        public SectionPainterToggle()
        {
            var content = EditorGUIUtility.TrTextContentWithIcon("", Tooltip, "d_Grid.PaintTool");
            text = content.text;
            tooltip = content.tooltip;
            icon = content.image as Texture2D;

            this.RegisterValueChangedCallback(Toggle);

            OnSelectionChanged();

            // keep track of panel events
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        protected virtual void OnAttachToPanel(AttachToPanelEvent evt)
        {
            SectionPainter.ActiveStatusChanged += OnSectionPainterActiveStatusChanged;
            SectionMarker.ActiveStatusChanged += OnSectionMarkerActiveStatusChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        protected virtual void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            SectionPainter.ActiveStatusChanged -= OnSectionPainterActiveStatusChanged;
            SectionMarker.ActiveStatusChanged -= OnSectionMarkerActiveStatusChanged;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSectionMarkerActiveStatusChanged(bool active)
        {
            SetEnabled(!active);
        }

        private void OnSelectionChanged()
        {
            if (SectionPainter.IsActive()) SectionPainter.Leave();
            SetEnabled(SelectionValid());
        }

        private bool SelectionValid()
        {
            // TODO: Optimize with less GetComponent calls.
            return Selection.activeObject != null &&
                   Selection.activeGameObject != null &&
                   Selection.activeGameObject.activeSelf &&
                   Selection.activeGameObject.GetComponent<MeshRenderer>() != null && // required for vertex painting
                   Selection.activeGameObject.GetComponent<MeshRenderer>().enabled && // required for vertex painting
                   Selection.activeGameObject.GetComponent<MeshFilter>() != null && // required for intersection test
                   Selection.activeGameObject.GetComponent<MeshRenderer>().sharedMaterial.HasProperty("_SdfSectioningTexture"); // required for authoring sdf texture
        }

        private void OnSectionPainterActiveStatusChanged(bool active)
        {
            SetValueWithoutNotify(active);
        }

        private void Toggle(ChangeEvent<bool> evt)
        {
            if (evt.newValue)
            {
                // NOTE: File names are not unique within the assets!!
                
                // Set paths.
                const string defaultSectionTextureFolderPath = "Assets/Rendering/Sectioning Textures";
                const string defaultSectionSourceTextureFolderPath = "Assets/Rendering/Sectioning Textures/Sources";

                // Get material information.
                var selectedGameObject = Selection.activeGameObject;
                var material = selectedGameObject.GetComponent<MeshRenderer>().sharedMaterial;
                var materialGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material.GetInstanceID()));

                // Get the assigned _SdfSectioningTexture for this material.
                var sectionTexture = material.GetTexture("_SdfSectioningTexture");
                if (sectionTexture != null)
                {
                    // Get information about the location and path of the assigned _SdfSectioningTexture.
                    var sectionTexturePath = AssetDatabase.GetAssetPath(sectionTexture.GetInstanceID()); // Assets/Rendering/Sectioning Textures/section_texture.png
                    var sectionTextureFileNameWithoutExtension = Path.GetFileNameWithoutExtension(sectionTexturePath); // section_texture
                    var sectionTextureFileExtension = Path.GetExtension(sectionTexturePath); // png
                    var sectionTextureDirectoryName = Path.GetDirectoryName(sectionTexturePath); // Assets/Rendering/Sectioning Textures

                    // Check if a source texture exists by looking for the section_texture_source.png file.
                    var sectionSourceTexturePath = sectionTextureDirectoryName + "/sources/" + sectionTextureFileNameWithoutExtension + "_source" + sectionTextureFileExtension;
                    var sectionSourceTextureFound = AssetDatabase.GetMainAssetTypeAtPath(sectionSourceTexturePath) != null;
                    
                    // If a source was found, enter the section painter by loading the source texture.
                    if (sectionSourceTextureFound)
                    {
                        var sectionSourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(sectionSourceTexturePath);
                        SectionPainter.Enter(material, materialGuid, sectionTexturePath, sectionSourceTexturePath, sectionSourceTexture);
                    }
                    
                    // If no source was found, then enter the section painter without an existing source texture.
                    else
                    {
                        SectionPainter.Enter(material, materialGuid, sectionTexturePath, sectionSourceTexturePath);
                    }
                }
                
                // If no _SdfSectioningTexture was assigned yet for this material then create it.
                else
                {
                    // Create necessary directories if they do not exist yet.
                    Directory.CreateDirectory(defaultSectionTextureFolderPath);
                    Directory.CreateDirectory(defaultSectionSourceTextureFolderPath);
                    
                    var sectionTextureName = "section_texture_" + materialGuid + ".png";
                    var sectionSourceTextureName = "section_texture_" + materialGuid + "_source.png";

                    var sectionTexturePath = Path.Combine(defaultSectionTextureFolderPath, sectionTextureName);
                    var sectionSourceTexturePath = Path.Combine(defaultSectionSourceTextureFolderPath, sectionSourceTextureName);
                    
                    // Create a new _SdfSectioningTexture.
                    var tex = new Texture2D(128, 128, TextureFormat.RGB24, false, false);
                    File.WriteAllBytes(sectionTexturePath, tex.EncodeToPNG());
                    AssetDatabase.ImportAsset(sectionTexturePath);
                    

                    material.SetTexture("_SdfSectioningTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(sectionTexturePath));
                                        
                    Debug.Log("No _SdfSectioningTexture was assigned to this material yet. It has been created at " + sectionTexturePath);
                    
                    
                    
                    // Clean up.
                    if (Application.isPlaying)
                        Object.Destroy(tex);
                    else
                        Object.DestroyImmediate(tex);

                    // Enter the section painter
                    SectionPainter.Enter(material, materialGuid, sectionTexturePath, sectionSourceTexturePath);
                    
                    //SetValueWithoutNotify(!evt.newValue);
                }


            }
            else SectionPainter.Leave();
        }
    }

    [EditorToolbarElement(ID, typeof(SceneView))]
    public class SectionEditorContextLabel : VisualElement
    {
        public const string ID = SectionEditorToolbarOverlay.OverlayID + "/context-label";

        private readonly LabelWithIcon gameobjectLabel;
        private readonly LabelWithIcon meshLabel;
        private readonly LabelWithIcon vertexCountLabel;
        private readonly LabelWithIcon triangleCountLabel;

        
        // TODO: Add this in a vertical group or something.

        public SectionEditorContextLabel()
        {
            gameobjectLabel = new LabelWithIcon("d_GameObject Icon");
            meshLabel = new LabelWithIcon("d_Mesh Icon");
            vertexCountLabel = new LabelWithIcon("d_EdgeCollider2D Icon");
            triangleCountLabel = new LabelWithIcon("CompositeCollider2D Icon");
            
            Add(gameobjectLabel);
            Add(meshLabel);
            Add(vertexCountLabel);
            Add(triangleCountLabel);


            // keep track of panel events
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            // initialize the label
            UpdateContextLabel();
        }

        protected virtual void OnAttachToPanel(AttachToPanelEvent evt)
        {
            // subscribe to events
            Selection.selectionChanged += UpdateContextLabel;
            EditorApplication.hierarchyChanged += UpdateContextLabel;
        }

        protected virtual void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            // unsubscribe from events
            Selection.selectionChanged -= UpdateContextLabel;
            EditorApplication.hierarchyChanged -= UpdateContextLabel;
        }

        private void UpdateContextLabel()
        {
            var selectedGameObjectName = "Unknown";
            var selectedMeshName = "Unknown";
            var selectedMeshTriangleCount = 0;
            var selectedMeshVertexCount = 0;

            if (SectionMarker.IsActive() && SectionMarker.SelectedGameObject != null)
            {
                selectedGameObjectName = SectionMarker.SelectedGameObject.name;
                if (SectionMarker.SelectedGameObject.TryGetComponent(out MeshFilter meshFilter))
                {
                    if (meshFilter.sharedMesh != null)
                    {
                        var sharedMesh = meshFilter.sharedMesh;
                        selectedMeshName = sharedMesh.name;
                        selectedMeshTriangleCount = sharedMesh.triangles.Length / 3;
                        selectedMeshVertexCount = sharedMesh.vertices.Length;
                    }
                }
            }
            else if (Selection.activeGameObject != null)
            {
                selectedGameObjectName = Selection.activeGameObject.name;
                if (Selection.activeGameObject.TryGetComponent(out MeshFilter meshFilter))
                {
                    if (meshFilter.sharedMesh != null)
                    {
                        var sharedMesh = meshFilter.sharedMesh;
                        
                        selectedMeshName = sharedMesh.name;
                        selectedMeshTriangleCount = sharedMesh.triangles.Length / 3;
                        selectedMeshVertexCount = sharedMesh.vertices.Length;
                    }
                }
            }

            gameobjectLabel.Text = selectedGameObjectName;
            meshLabel.Text = selectedMeshName;
            triangleCountLabel.Text = selectedMeshTriangleCount.ToString();
            vertexCountLabel.Text = selectedMeshVertexCount.ToString();
        }
    }
}