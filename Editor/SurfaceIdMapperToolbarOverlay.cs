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
            SurfaceIdMapperToggle.ID
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
            if (!visible) return;
        }

        private void OnSelectionChanged()
        {
            visible = IsSelectionValid();
        }

        private static bool IsSelectionValid()
        {
            // TODO: Optimize with less GetComponent calls.
            return SurfaceIdMapper.IsActive() ||
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
            var content = EditorGUIUtility.TrTextContentWithIcon("", Tooltip, "d_NetworkIdentity Icon");
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
            SurfaceIdMapper.ToolActiveStatusChanged += OnSectionMarkerToolActiveStatusChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnSectionMarkerToolActiveStatusChanged(bool active)
        {
            SetValueWithoutNotify(active);
        }

        protected virtual void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            SurfaceIdMapper.ToolActiveStatusChanged -= OnSectionMarkerToolActiveStatusChanged;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (SurfaceIdMapper.IsActive()) SurfaceIdMapper.Leave();
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
            if (evt.newValue) SurfaceIdMapper.Enter();
            else SurfaceIdMapper.Leave();
        }
    }
}