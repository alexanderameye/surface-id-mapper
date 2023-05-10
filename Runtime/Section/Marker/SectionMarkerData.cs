using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Ameye.OutlinesToolkit.Sectioning.Marker
{
    // https://github.com/SixWays/FacePaint/blob/master/Scripts/FacePaintData.cs
    
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SectionMarkerData : MonoBehaviour
    {
        [SerializeField] private Color[] vertexColors;
        
        private Color[] VertexColors
        {
            get => vertexColors ??= mesh.colors;
            set => vertexColors = mesh.colors = value;
        }

        private Mesh mesh;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        private MeshRenderer Renderer
        {
            get
            {
                if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
                return meshRenderer;
            }
        }

        private MeshFilter Filter
        {
            get
            {
                if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
                return meshFilter;
            }
        }

        
        private void Reset()
        {
            // Called when the user hits the Reset button in the Inspector's context menu or when adding the component for the first time.
            InitializeMesh();
            SetColor(Color.red);
        }


        public void Rebuild()
        {
            InitializeMesh();
            SetColor(Color.red);
        }
        private void Awake()
        {
            InitializeMesh();
            ApplyColors();
        }

        private void OnDestroy()
        {
            CleanUpMesh();
            if (Renderer != null) Renderer.additionalVertexStreams = null;
        }

        private void InitializeMesh()
        {
            CleanUpMesh();
            
            // Create a new mesh for additionalVertexStreams.
            mesh = new Mesh
            {
                vertices = Filter.sharedMesh.vertices
            };
            Renderer.additionalVertexStreams = mesh;
        }

        private void CleanUpMesh()
        {
            if (!mesh) return;
            DestroyImmediate(mesh);
        }

        public Color[] GetColors()
        {
            return VertexColors;
        }
        
        public void SetColors(Color[] colors)
        {
            VertexColors = colors;
            ApplyColors();
        }
        
        public void SetColor(Color color)
        {
            VertexColors = Filter.sharedMesh.colors;
            if (VertexColors == null || VertexColors.Length != Filter.sharedMesh.vertexCount)
            {
                VertexColors = new Color[Filter.sharedMesh.vertexCount];
                for (var i = 0; i < VertexColors.Length; ++i)
                {
                    VertexColors[i] = color;
                }
            }
            ApplyColors();
        }

        public void ApplyColors()
        {
            if (vertexColors is {Length: > 0}) mesh.SetColors(new List<Color>(VertexColors));
        }
    }
}