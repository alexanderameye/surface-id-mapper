using System.Collections.Generic;
using UnityEngine;

namespace Ameye.SurfaceIdMapper.Section.Marker
{
    // https://github.com/SixWays/FacePaint/blob/master/Scripts/FacePaintData.cs
    // https://github.com/needle-mirror/com.unity.polybrush/blob/5da6404a35b2bcba05009a091745be6b3667c3c2/Runtime/Scripts/MonoBehaviour/PolybrushMesh.cs
    
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SurfaceIdMapData : MonoBehaviour
    {
        // Meshes.
        private Mesh mesh;
        private Mesh stream;
        
        // Stream data.
        [SerializeField] private Color[] vertexColors;
        
        private ComponentsCache componentsCache;
        
        private Color[] VertexColors
        {
            get => vertexColors ??= stream.colors;
            set => vertexColors = stream.colors = value;
        }
        
        /// <summary>
        /// References components needed: MeshFilter and MeshRenderer.
        /// Will cache references if they are found to avoid additional GetComponent() calls.
        /// </summary>
        private readonly struct ComponentsCache
        {
            private readonly GameObject owner;

            internal bool IsValid()
            {
                return owner != null;
            }

            internal MeshFilter MeshFilter { get; }

            internal MeshRenderer MeshRenderer { get; }

            internal ComponentsCache(GameObject root)
            {
                owner = root;
                MeshFilter = root.GetComponent<MeshFilter>();
                MeshRenderer = root.GetComponent<MeshRenderer>();
            }
        }
        
        /// <summary>
        /// Returns true if internal data has been initialized.
        /// If returns false, use <see cref="Initialize"/>.
        /// </summary>
        private bool IsInitialized { get; set; }

        private void Initialize()
        {
            if (IsInitialized) return;

            // Initialize components cache.
            if (!componentsCache.IsValid()) componentsCache = new ComponentsCache(gameObject);

            // Create a new vertex stream that holds the vertex color data.
            mesh = componentsCache.MeshFilter.sharedMesh;
            if (stream == null) stream = new Mesh();
            stream.MarkDynamic();
            stream.vertices = mesh.vertices;
            stream.triangles = mesh.triangles;
            vertexColors = new Color[mesh.vertexCount];
            for (var i = 0; i < vertexColors.Length; i++) vertexColors[i] = Color.white;
            stream.colors = vertexColors;
            componentsCache.MeshRenderer.additionalVertexStreams = stream;
            
            IsInitialized = true;
            Debug.Log("Initialized Surface ID Map Data.");
        }
        
        /// <summary>
        /// Called when the user hits the Reset button in the inspector context menu.
        /// Called when adding the component for the first time.
        /// </summary>
        private void Reset()
        {
            Initialize();
            SetColor(Color.red);
        }

        /// <summary>
        /// Force rebuild the surface map ID data.
        /// </summary>
        public void Rebuild() => Reset();

        private void Awake()
        {
            Initialize();
            stream.SetColors(vertexColors);
        }

        public void Apply()
        {
            if (vertexColors is {Length: > 0}) stream.colors = vertexColors;
        }

        private void OnDestroy()
        {
            if (stream != null) DestroyImmediate(stream);
            if (componentsCache.IsValid()) componentsCache.MeshRenderer.additionalVertexStreams = null;
        }
        
        public Color[] GetColors()
        {
            return VertexColors;
        }
        
        public void SetColors(Color[] colors)
        {
            vertexColors = colors;
            stream.SetColors(colors);
        }
        
        public void SetColor(Color color)
        {
            // Get the vertex colors of the original mesh.
            vertexColors = new Color[mesh.vertexCount];
            for (var i = 0; i < vertexColors.Length; i++) vertexColors[i] = color;
            
            stream.colors = vertexColors;
        }
    }
}