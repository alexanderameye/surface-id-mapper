using Ameye.OutlinesToolkit.Editor.Sectioning.Enums;
using Ameye.OutlinesToolkit.Editor.Sectioning.Utilities;
using Ameye.SurfaceIdMapper.Editor.Utilities;
using Ameye.SurfaceIdMapper.Section.Marker;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ameye.SurfaceIdMapper.Editor
{
    [CustomEditor(typeof(SurfaceIdMapData))]
    public class SurfaceIdMapDataEditor : UnityEditor.Editor
    {
        private readonly GUIContent info =
            EditorGUIUtility.TrTextContent(
                "This component contains section marker data (vertex colors)." +
                "\nDelete this component to reset the mesh to its original vertex colors." +
                "\nCopy/paste this component to another gameobject with a MeshRenderer component to apply the same section paint data (vertex colors).");
       
        public VisualTreeAsset visualTreeAsset;
        public StyleSheet styleSheet;

        private Button fillButton, randomizeButton, setOccluderButton;
        private Button rebuildDataButton;
        private ProgressBar progressBar;
        private SurfaceIdMapData markerData;

        private VisualElement headerIcon;

        public override VisualElement CreateInspectorGUI()
        {
            markerData = (SurfaceIdMapData) target;
            
            var root = new VisualElement();
            
            visualTreeAsset.CloneTree(root);
            
            root.styleSheets.Add(styleSheet);
            
          

            var helpBox = new HelpBox();
            helpBox.text = info.text;
            helpBox.messageType = HelpBoxMessageType.None;
            helpBox.style.marginLeft = 0.0f;
            helpBox.style.marginRight = 0.0f;
            helpBox.style.paddingBottom = 5.0f;
            helpBox.style.paddingLeft = 5.0f;
            helpBox.style.paddingRight = 5.0f;
            helpBox.style.paddingTop = 5.0f;
            root.Add(helpBox);


            headerIcon = root.Q<VisualElement>("header-icon");
            fillButton = root.Q<Button>("fill-colors-button");
            fillButton.clickable.clicked += OnFillButtonClicked;
            randomizeButton = root.Q<Button>("randomize-colors-button");
            randomizeButton.clickable.clicked += OnRandomizeButtonClicked;
            setOccluderButton = root.Q<Button>("set-occluder-button");
            setOccluderButton.clickable.clicked += OnSetOccluderButtonClicked;
            
            rebuildDataButton = root.Q<Button>("rebuild-data-button");
            rebuildDataButton.clickable.clicked += OnRebuildDataButtonClicked;
            
            headerIcon.AddToClassList("header-icon");
            
            headerIcon.style.width = 16;
            headerIcon.style.height = 16;

            //progressBar = root.Q<ProgressBar>("progress-bar");
            
            return root;
        }

        private void OnRebuildDataButtonClicked()
        {
            markerData.Rebuild();
        }
        
        private void OnRandomizeButtonClicked()
        {
            var gameObject = markerData.gameObject;
            var mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
            SurfaceIdMapperUtility.SetSectionMarkerDataForMesh(markerData, mesh, Channel.R, SectionMarkMode.Random);
        }

        private void OnSetOccluderButtonClicked()
        {
            markerData.SetColor(Color.black);
        }
        
        private void OnFillButtonClicked()
        {
            markerData.SetColor(Color.red);
        }
    }
}