using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ameye.OutlinesToolkit.Editor.Sectioning.Enums;
using Ameye.SurfaceIdMapper.Section.Marker;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Ameye.SurfaceIdMapper.Editor.Utilities
{
    public static class SurfaceIdMapperUtility
    {
        public static Color32 GetRandomSectionColorForChannel(Channel channel)
        {
            // NOTE: Here we avoid the value of 0 since that is reserved for occluders.
            var randomValue = BitConverter.GetBytes(Random.Range(1, 255));

            return new Color32(
                channel == Channel.R ? randomValue[0] : (byte) 0,
                channel == Channel.G ? randomValue[0] : (byte) 0,
                channel == Channel.B ? randomValue[0] : (byte) 0,
                255);
        }

        private static Color32 GetSequentialSectionColorForChannel(ref int index, Channel channel)
        {
            var value = BitConverter.GetBytes(index);
            index++;

            return new Color32(
                channel == Channel.R ? value[0] : (byte) 0,
                channel == Channel.G ? value[0] : (byte) 0,
                channel == Channel.B ? value[0] : (byte) 0,
                255);
        }

        public static void ModifyColorForChannel(ref Color color, Color newColor, Channel channel)
        {
            switch (channel)
            {
                case Channel.R:
                    color.r = newColor.r;
                    break;
                case Channel.G:
                    color.g = newColor.g;
                    break;
                case Channel.B:
                    color.b = newColor.b;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }

        public static void FillMarkerDataWithColor(SurfaceIdMapData data, Mesh mesh, Channel channel, Color color)
        {
            // Performance timing start.
            var stopwatch = Stopwatch.StartNew();
            var colors = data.GetColors();
            // WARN: Does not go through sub-meshes.
            for (var i = 0; i < mesh.vertexCount; ++i) ModifyColorForChannel(ref colors[i], color, channel);
            data.SetColors(colors);
            // Performance timing stop.
            stopwatch.Stop();
            Debug.Log("SetSectionMarkerDataForMesh [" + stopwatch.ElapsedMilliseconds + "ms],");
        }

        public static void SetSectionMarkerDataForMesh(SurfaceIdMapData data, Mesh mesh, Channel channel, SectionMarkMode mode)
        {
            // NOTE: All triangles are handled as index buffers so a single triangle takes up 3 elements.
            
            // Performance timing start.
            var stopwatch = Stopwatch.StartNew();
            
            // Get colors.
            var colors = data.GetColors();
            var visitedTriangles = new Dictionary<(int index0, int index1, int index2), bool>();
            
            var assignedColorIndex = 1;

            // Get vertices.
            var vertices = new List<Vector3>(mesh.vertexCount);
            mesh.GetVertices(vertices);

            int[] triangles = mesh.triangles;
            Color color = new Color(0.0f, 0.0f, 0.0f);
            var connectedTrianglesIndexBuffer = new List<int>();
            
            // Loop through triangles.
            for (var triangleIndex = 0; triangleIndex < triangles.Length; triangleIndex += 3)
            {
                int[] triangle = { triangles[triangleIndex], triangles[triangleIndex + 1], triangles[triangleIndex + 2] };

                // If this triangle is part of an already processed connected section, skip it.
                if (visitedTriangles.ContainsKey((triangle[0], triangle[1], triangle[2]))) continue;
                
                // Performance timing start.
                connectedTrianglesIndexBuffer = GetConnectedTriangles(triangle, mesh.triangles.ToList());
                
                // Generate color for this section.
                color = mode == SectionMarkMode.Random
                    ? GetRandomSectionColorForChannel(channel)
                    : GetSequentialSectionColorForChannel(ref assignedColorIndex, channel);

                // Modify color for all connected triangles.
                for (var t = 0; t < connectedTrianglesIndexBuffer.Count; t += 3)
                {
                    var index0 = connectedTrianglesIndexBuffer[t];
                    var index1 = connectedTrianglesIndexBuffer[t+1];
                    var index2 = connectedTrianglesIndexBuffer[t+2];
                        
                    // If the connected triangle is a duplicate, don't bother adding it.
                    if (visitedTriangles.ContainsKey((index0, index1, index2))) continue;
                    
                    // Set section color for all 3 vertices of the triangle.
                    ModifyColorForChannel(ref colors[index0], color, channel);
                    ModifyColorForChannel(ref colors[index1], color, channel);
                    ModifyColorForChannel(ref colors[index2], color, channel);

                    // Remember triangle.
                    visitedTriangles.Add((index0, index1, index2), true);
                }
                
            }

            // Apply colors.
            data.SetColors(colors);
      
            // Performance timing stop.
            stopwatch.Stop();
            Debug.Log("SetSectionMarkerDataForMesh [" + stopwatch.ElapsedMilliseconds + "ms],");
        }
        
        public static SurfaceIdMapData GetOrAddSurfaceIdMapData(GameObject gameObject)
        {
            if (gameObject == null) Debug.LogError("Trying to get surface ID map data for null gameobject.");
            if (gameObject.TryGetComponent(out SurfaceIdMapData data)) return data;
            
            // If no surface ID map data has been added yet, add it and initialize the data with a default color.
            // the default color is black but with an R component of 1 so it is not seen as an occluder.
            data = gameObject.AddComponent<SurfaceIdMapData>();
          //  data.Initialize();

            return data;
        }

        public static List<int> GetConnectedTriangles(int[] triangle, List<int> triangles,
            HashSet<(int index0, int index1, int index2)> addedTriangles = null)
        {
            // NOTE: For some reason, the returned list could contain duplicates so we pass a dictionary here to filter out the duplicates.
            // NOTE: connected triangles is returned as a flat list of ints, but pairs of 3 make up a triangle
            if (addedTriangles == null)
            {
                addedTriangles = new HashSet<(int index0, int index1, int index2)>();
                addedTriangles.Add((triangle[0], triangle[1], triangle[2]));
            }

            // important: the allTriangles list could contain duplicates so the returned index buffer could also contain duplicates
            // todo: filter on these? so use a dictionary and then if duplicate don't bother checking it? but with recursion looks a bit tricky


            var connectedTrianglesIndexBuffer = new List<int>(triangle);
        
            var currentTriangle = new int[3];
            
            // Go through all triangles. Since we are using an index buffer, we are doing iteration steps of 3.
            for (var i = 0; i < triangles.Count; i += 3)
            {
                // Select the (i)th triangle from the index buffer.
                currentTriangle[0] = triangles[i];
                currentTriangle[1] = triangles[i + 1];
                currentTriangle[2] = triangles[i + 2];
                
                // Check if the triangles are connected.
                if (AreTrianglesConnected(currentTriangle, triangle))
                {
                    // Recursively add all the linked faces to the result
                    if (addedTriangles.Contains((currentTriangle[0], currentTriangle[1], currentTriangle[2]))) continue;
                    
                    addedTriangles.Add((currentTriangle[0], currentTriangle[1], currentTriangle[2]));
                    connectedTrianglesIndexBuffer.AddRange(GetConnectedTriangles(currentTriangle, triangles, addedTriangles));
                }
            }

            return connectedTrianglesIndexBuffer;
        }

        /// <summary>
        /// Returns whether or not two triangles are connected.
        /// </summary>
        /// <param name="faceA"></param>
        /// <param name="faceB"></param>
        /// <param name="triangleA"></param>
        /// <param name="triangleB"></param>
        /// <returns></returns>
        /*  private static bool AreTrianglesConnected(int[] faceA, int[] faceB)
        {
            for (var i = 0; i < faceA.Length; i++)
            for (var j = 0; j < faceB.Length; j++)
            {
                if (faceA[i] == faceB[j])
                    return true;
            }
            return false;
        }*/
        
        private static bool AreTrianglesConnected(IReadOnlyList<int> triangleA, IReadOnlyList<int> triangleB)
        {
            // Loop through vertices.
            for (var i = 0; i < 3; i++)
            for (var j = 0; j < 3; j++)
            {
                if (triangleA[i] == triangleB[j]) return true;
            }
            return false;
        }

        private static bool AreNormalsWithinRange(Vector3 normalA, Vector3 normalB, float angle)
        {
            return Vector3.Angle(normalA, normalB) <= angle;
        }
    }
}