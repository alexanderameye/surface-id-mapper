using Ameye.SurfaceIdMapper.Section.Marker;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ameye.SurfaceIdMapper.Editor
{
    /// <summary>
    /// Custom Editor for SurfaceIdMapData.
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(AdditionalVertexStream))]
    public class AdditionalVertexStreamEditor : UnityEditor.Editor
    {
        private static class Styles
        {
            internal static readonly GUIContent ComponentInfo = EditorGUIUtility.TrTextContent("This component holds additional vertex attributes for a mesh.");
            internal static readonly GUIContent AdditionalVertexStreamsLabel = EditorGUIUtility.TrTextContent("Vertex Stream");
            internal static readonly GUIContent RebuildDataButton = EditorGUIUtility. TrTextContent("Rebuild Stream");
            internal static readonly GUIContent InvalidateIslandDataButton = EditorGUIUtility. TrTextContent("Invalidate Islands");
        }
        
        public VisualTreeAsset visualTreeAsset;
        public StyleSheet styleSheet;

        private Button fillButton, randomizeButton, setOccluderButton;
        private Button rebuildDataButton;
        private ProgressBar progressBar;
        private AdditionalVertexStream data;

        private VisualElement headerIcon;

        public override void OnInspectorGUI()
        {
            data = target as AdditionalVertexStream;
            
            GUI.enabled = false;
            if (data.MeshRenderer != null) EditorGUILayout.ObjectField(Styles.AdditionalVertexStreamsLabel, data.MeshRenderer.additionalVertexStreams, typeof(Mesh), true);
            if (data.MeshRenderer.additionalVertexStreams == null)
            {
               
                    EditorGUILayout.HelpBox("The additionalVertexStreams for this MeshRenderer is null. This is probably cause by a change to the mesh.", MessageType.Error);
                
            }
            if (data.IsIslandDataComputed)
            {
                EditorGUILayout.LabelField("Surface mapper found " + data.NumberOfIslands + " islands.");
            }
            else
            {
                EditorGUILayout.HelpBox("Islands have not been calculated.", MessageType.Warning);
            }
            GUI.enabled = true;
            
            
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Styles.RebuildDataButton)) data.RebuildStream();
                if (GUILayout.Button(Styles.InvalidateIslandDataButton)) data.InvalidateIslandData();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(Styles.ComponentInfo.text, MessageType.Info);
            EditorGUILayout.Space();
        }

        /*public override VisualElement CreateInspectorGUI()
        {
            data = target as SurfaceIdMapData;
            
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
        }*/

      /*  private void OnRebuildDataButtonClicked()
        {
            data.Rebuild();
        }
        
        private void OnRandomizeButtonClicked()
        {
            var gameObject = data.gameObject;
            var mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
            SurfaceIdMapperUtility.SetSectionMarkerDataForMesh(data, mesh, Channel.R, SectionMarkMode.Random);
        }

        private void OnSetOccluderButtonClicked()
        {
            data.SetColor(Color.black);
        }
        
        private void OnFillButtonClicked()
        {
            data.SetColor(Color.red);
        }*/
    }
}