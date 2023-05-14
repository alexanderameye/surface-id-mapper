using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

namespace Ameye.SurfaceIdMapper.Section.Marker
{
    // https://github.com/SixWays/FacePaint/blob/master/Scripts/FacePaintData.cs
    // https://github.com/needle-mirror/com.unity.polybrush/blob/5da6404a35b2bcba05009a091745be6b3667c3c2/Runtime/Scripts/MonoBehaviour/PolybrushMesh.cs
    
    /// <summary>
    /// Component that holds additional vertex attributes for a mesh.
    /// </summary>
    [DisallowMultipleComponent, ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class AdditionalVertexStream : MonoBehaviour
    {
        // Serialized stream data.
        // NOTE: Needs to be serialized so that undo/redo can be performed on it.
        [SerializeField] private Color[] colors;
        
        // Meshes.
        private Mesh mesh;
        [SerializeField] private Mesh stream; // TODO: Convert this to a more minimal mesh with a different data structure that allows rapid operations on it.

        private ComponentCache componentCache;
        
        // Island data.
        // NOTE: Unity can't serialize a list of lists so need to do it this way.
        [System.Serializable]
        private class Island: List<int> {}
        [SerializeField]
        private List<Island> islands;
        
        
        private Dictionary<(int, int, int), int> islandLookup;

        /// <summary>
        /// Has the island data been computer or not?
        /// </summary>
        public bool IsIslandDataComputed => islands != null;
        
        public void InvalidateIslandData()
        {
            islands = null;
            islandLookup = null;
        }

        public MeshRenderer MeshRenderer
        {
            get
            {
                if (!componentCache.IsValid()) componentCache = new ComponentCache(gameObject);
                return componentCache.MeshRenderer; 
            }
        }
        
        public MeshFilter MeshFilter
        {
            get
            {
                if (!componentCache.IsValid()) componentCache = new ComponentCache(gameObject);
                return componentCache.MeshFilter; 
            }
        }

        public Color[] VertexColors 
        {
            get => colors ??= stream.colors;
            set => colors = stream.colors = value;
        }
        
        /// <summary>
        /// References components needed: MeshFilter and MeshRenderer.
        /// Will cache references if they are found to avoid additional GetComponent() calls.
        /// </summary>
        private readonly struct ComponentCache
        {
            private readonly GameObject owner;

            internal bool IsValid()
            {
                return owner != null;
            }

            internal MeshFilter MeshFilter { get; }

            internal MeshRenderer MeshRenderer { get; }

            internal ComponentCache(GameObject root)
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
            if (!componentCache.IsValid()) componentCache = new ComponentCache(gameObject);

            // Create a new vertex stream that holds the vertex color data.
            mesh = componentCache.MeshFilter.sharedMesh;
            if (stream == null) stream = new Mesh();
            stream.MarkDynamic();
            stream.vertices = mesh.vertices;
            stream.triangles = mesh.triangles;
            colors = new Color[mesh.vertexCount];
            for (var i = 0; i < colors.Length; i++) colors[i] = Color.white;
            stream.colors = colors;
            componentCache.MeshRenderer.additionalVertexStreams = stream;
            componentCache.MeshRenderer.additionalVertexStreams.name = mesh.name + " (AVS)";
            stream.hideFlags = HideFlags.HideAndDontSave;
            
            IsInitialized = true;
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
            //OnUndoRedo();
        }
        
        private void OnDestroy()
        {
            if (stream != null) DestroyImmediate(stream);
            if (componentCache.IsValid()) componentCache.MeshRenderer.additionalVertexStreams = null;
        }
        
        public void SetColors(Color[] colors)
        {
            Undo.RecordObject(this, "Apply stream vertex colors.");
            this.colors = colors;
            Apply();
        }
        
        public void SetColor(Color color)
        {
            Undo.RecordObject(this, "Apply stream vertex colors.");
            colors = new Color[mesh.vertexCount];
            for (var i = 0; i < colors.Length; i++) colors[i] = color;
            Apply();
        }

        public void OnUndoRedo() => Apply();

        private void Apply()
        {
            if(colors is {Length: > 0}) stream.colors = colors;
        }
        
        /// <summary>
        /// Returns whether a triangle is mapped to an island given a triangle and a list of islands.
        /// </summary>
        /// <param name="islands"></param>
        /// <param name="triangle"></param>
        /// <returns></returns>
        private static bool IsTriangleMappedToIsland(List<Island> islands, int[] triangle){
            // If no islands have been generated, a triangle could not have been mapped yet.
            if (islands == null || islands.Count == 0) return false;

            // Loop through the islands.
            foreach (var island in islands)
            {
                for (var i = 0; i < island.Count; i += 3)
                {
                    if (triangle[0] == island[i] &&
                        triangle[1] == island[i + 1] &&
                        triangle[2] == island[i + 2]) return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Get a list of islands where an island is a list of triangles that are connected.
        /// The island itself is a list that contains the indices.
        /// You can get a specific vertex using tris[(index * 3) + 0,1,2]
        /// </summary>
        private void GenerateIslands()
        {
            islands = new List<Island>();
            islandLookup = new Dictionary<(int, int, int), int>();

            // Get the triangles.
            var triangles = MeshFilter.sharedMesh.triangles;
            
            var currentTriangle = new int[3];
            
            // Loop through the triangles.
            for (var i = 0; i < triangles.Length; i += 3)
            {
                // Select the (i)th triangle from the index buffer.
                currentTriangle[0] = triangles[i];
                currentTriangle[1] = triangles[i + 1];
                currentTriangle[2] = triangles[i + 2];
                
                // Skip the triangle if it is already mapped to an island.
                if(islandLookup.ContainsKey((currentTriangle[0], currentTriangle[1], currentTriangle[2]))) continue;
                //if (IsTriangleMappedToIsland(generatedIslands, currentTriangle)) continue;
                
                // Since the triangle does not belong to an island yet, create a new island.
                var island = new Island();
                island.AddRange(currentTriangle);
                islands.Add(island);
                
                // Get the current index of the island.
                var islandIndex = islands.Count - 1;
         
                // Recursively map connected triangles for this triangle.
                MapConnectedTriangles(islandLookup, islands, island, islandIndex, triangles, currentTriangle);
            }
        }
        
        /// <summary>
        /// Generates a lookup dictionary that takes in a triangle as a key (3 indices into the vertex buffer) and returns the
        /// index (into the islands list) of the island to which the triangle belongs to.
        /// </summary>
        /// <returns>A triangle -> island index lookup dictionary.</returns>
        private Dictionary<(int, int, int), int> GenerateIslandLookup(List<Island> islands)
        {
            var generatedIslandLookup = new Dictionary<(int, int, int), int>();
            
            // Loop through the islands.
            for (var islandIndex = 0; islandIndex < islands.Count; ++islandIndex)
            {
                // For each island, get the indices that belong to that island.
                List<int> indices = islands[islandIndex];
                
                // Loop through the indices in steps of 3.
                for (int index = 0; index < indices.Count; index += 3)
                {
                    var triangleKey = (indices[index], indices[index + 1], indices[index + 2]);
                    
                    // If the dictionary already contains this triangle, skip it.
                    if (generatedIslandLookup.ContainsKey(triangleKey)) continue;
                    
                    // Add the triangleKey => islandIndex mapping.
                    generatedIslandLookup.Add(triangleKey, islandIndex);
                }
            }
            return generatedIslandLookup;
        }

        /// <summary>
        /// Recursive function that maps connected triangle for a given island.
        /// </summary>
        /// <param name="island"></param>
        /// <param name="triangles"></param>
        /// <param name="targetTriangle"></param>
        private void MapConnectedTriangles(Dictionary<(int, int, int), int> lookup, List<Island> islands, Island island, int islandIndex, int[] triangles, int[] targetTriangle){
            
            var currentTriangle = new int[3];
            
            // Loop through the triangles.
            for (var i = 0; i < triangles.Length; i += 3)
            {
                // Select the (i)th triangle from the index buffer.
                currentTriangle[0] = triangles[i];
                currentTriangle[1] = triangles[i + 1];
                currentTriangle[2] = triangles[i + 2];

                var triangleKey = (currentTriangle[0], currentTriangle[1], currentTriangle[2]);
                
                // Skip the triangle itself.
                if(currentTriangle == targetTriangle) continue;
                
                // Skip the triangle if it has already been mapped to an island.
                if(lookup.ContainsKey(triangleKey)) continue;
                //if (IsTriangleMappedToIsland(islands, currentTriangle)) continue;

                // Check if the triangles are connected.
                if (AreTrianglesConnected(currentTriangle, targetTriangle))
                {
                    // The triangle is connected so add it to the island.
                    island.AddRange(currentTriangle);
                    
                    // Add the triangle to the lookup.
                    lookup.Add(triangleKey, islandIndex);
                    
                    // Recursively map triangles.
                    MapConnectedTriangles(lookup, islands, island, islandIndex, triangles, currentTriangle);
                }
            }
        }
        
        /// <summary>
        /// Given a triangle (3 indices into the vertex buffer), returns a list of connected triangles.
        /// </summary>
        /// <param name="triangle"></param>
        /// <returns>A list of connected triangles.</returns>
        public List<int> GetConnectedTriangles(int[] triangle)
        {
            // Regenerate if needed.
            if (islands == null || islands.Count == 0)
            {
                var generateIslands = Stopwatch.StartNew();
                GenerateIslands();
                generateIslands.Stop();
                Debug.Log("Generated islands in [" + generateIslands.ElapsedMilliseconds + "ms].");
                Debug.Log("The Surface ID Mapper found " + islands.Count + " islands.");
            }
            //if (islandLookup == null || islandLookup.Count == 0) islandLookup = GenerateIslandLookup(islands);
            
            // Do a lookup.
            if (!islandLookup.TryGetValue((triangle[0], triangle[1], triangle[2]), out var islandIndex)) return null;
            return islandIndex < islands.Count ? islands[islandIndex] : null;
        }
        
        /// <summary>
        /// Returns whether 2 triangles are connected or not.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private bool AreTrianglesConnected(int[] a, int[] b)
        {
            // Loop through vertices.
            for (var i = 0; i < 3; i++)
            for (var j = 0; j < 3; j++)
            {
                if (a[i] != b[j]) continue;
                //Debug.Log("Triangle (" + a[0] + ", " + a[1] + ", " + a[2]  +") is connected to (" + b[0] + ", " + b[1] + ", " + b[2] + ")");
                //Debug.Log("Because: "  +a[i] + " == " + b[j]);
                return true;
            }
            return false;
        }
    }
}