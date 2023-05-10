using System.IO;
using Ameye.OutlinesToolkit.Editor;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ameye.SurfaceIdMapper.Editor
{
    [Icon("d_SceneAsset Icon")]
    [Overlay(typeof(SceneView), OverlayID, "Surface ID Mapper")]
    public class SurfaceIdMapperToolbarOverlay : ToolbarOverlay, ITransientOverlay
    {
        public const string OverlayID = "surface-id-mapper-overlay";

        private SurfaceIdMapperToolbarOverlay() : base(
            SurfaceIdMapperToggle.ID,
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
            return Marker.SurfaceIdMapper.IsActive() ||
                   Selection.activeObject != null &&
                   Selection.activeGameObject != null &&
                   Selection.activeGameObject.activeSelf &&
                   Selection.activeGameObject.GetComponent<MeshRenderer>() != null && // required for vertex painting
                   Selection.activeGameObject.GetComponent<MeshRenderer>().enabled && // required for vertex painting
                   Selection.activeGameObject.GetComponent<MeshFilter>() != null; // required for intersection test
        }
    }

    [EditorToolbarElement(ID, typeof(SceneView))]
    public class SurfaceIdMapperToggle : EditorToolbarToggle
    {
        public const string ID = SurfaceIdMapperToolbarOverlay.OverlayID + "/surface-id-mapper-toggle";

        private const string Tooltip = "Toggle Surface ID Mapper.";

        public SurfaceIdMapperToggle()
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
            Marker.SurfaceIdMapper.ActiveStatusChanged += OnSectionMarkerActiveStatusChanged;
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
            Marker.SurfaceIdMapper.ActiveStatusChanged -= OnSectionMarkerActiveStatusChanged;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (Marker.SurfaceIdMapper.IsActive()) Marker.SurfaceIdMapper.Leave();
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
            if (evt.newValue) Marker.SurfaceIdMapper.Enter();
            else Marker.SurfaceIdMapper.Leave();
        }
    }

    [EditorToolbarElement(ID, typeof(SceneView))]
    public class SectionEditorContextLabel : VisualElement
    {
        public const string ID = SurfaceIdMapperToolbarOverlay.OverlayID + "/context-label";

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

            if (Marker.SurfaceIdMapper.IsActive() && Marker.SurfaceIdMapper.SelectedGameObject != null)
            {
                selectedGameObjectName = Marker.SurfaceIdMapper.SelectedGameObject.name;
                if (Marker.SurfaceIdMapper.SelectedGameObject.TryGetComponent(out MeshFilter meshFilter))
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