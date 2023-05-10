using System.Collections.Generic;
using UnityEngine;

namespace Ameye.OutlinesToolkit.Editor.Sectioning.Utilities
{
    public struct MeshIntersection
    {
        public bool found;
        public GameObject gameObject;
        public float distance;
        public Vector3 position;
        public Vector3 normal;
        public Vector3 vertex0, vertex1, vertex2;
        public int index0, index1, index2;
        public Vector3 normal0, normal1, normal2;

        public void Reset()
        {
            this = new MeshIntersection();
        }

        public bool Raycast(Ray worldRay)
        {
            var meshRenderers = Object.FindObjectsOfType<MeshRenderer>();
            return Raycast(worldRay, meshRenderers);
        }

        private bool Raycast(Ray worldRay, MeshRenderer meshRenderer)
        {
            Debug.Log("raycast performed");
            if (meshRenderer.bounds.IntersectRay(worldRay))
            {
                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                return Raycast(worldRay, meshFilter);
            }
            Reset();
            return false;
        }

        public bool Raycast(Ray worldRay, MeshFilter meshFilter)
        {
            if (meshFilter != null)
            {
                var mesh = meshFilter.sharedMesh;
                var meshTransform = meshFilter.transform;
                if (Raycast(worldRay, mesh, meshTransform))
                {
                    gameObject = meshFilter.gameObject;
                    return true;
                }
            }
            Reset();
            return false;
        }

        private bool Raycast(Ray worldRay, IEnumerable<MeshRenderer> meshRenderers)
        {
            Reset();
            var meshIntersection = default(MeshIntersection);
            var nearestDistance = Mathf.Infinity;
            foreach (var meshRenderer in meshRenderers)
            {
                if (meshIntersection.Raycast(worldRay, meshRenderer) &&
                    meshIntersection.distance < nearestDistance)
                {
                    nearestDistance = meshIntersection.distance;
                    this = meshIntersection;
                }
            }
            return found;
        }

        private bool Raycast(Ray worldRay, Mesh mesh, Transform meshTransform)
        {
            if (meshTransform != null)
            {
                var meshMatrix = meshTransform.localToWorldMatrix;
                return Raycast(worldRay, mesh, meshMatrix);
            }
            Reset();
            return false;
        }

        private bool Raycast(Ray worldRay, Mesh mesh, Matrix4x4 meshMatrix)
        {
            Reset();
            distance = Mathf.Infinity;
            var meshScale = meshMatrix.lossyScale;
            var normalScale =
                meshScale.x * meshScale.y * meshScale.z < 0
                    ? -1f
                    : +1f;
            var indices = mesh.triangles;
            var normals = mesh.normals;
            var vertices = mesh.vertices;
            var n = indices.Length;
            for (var i = 0; i < n;)
            {
                var i0 = indices[i++];
                var i1 = indices[i++];
                var i2 = indices[i++];

                var v0 = vertices[i0];
                var v1 = vertices[i1];
                var v2 = vertices[i2];

                v0 = meshMatrix.MultiplyPoint(v0);
                v1 = meshMatrix.MultiplyPoint(v1);
                v2 = meshMatrix.MultiplyPoint(v2);

                var plane = new Plane(v0, v1, v2);

                var hitDistance = 0f;
                if (!plane.Raycast(worldRay, out hitDistance))
                    continue;

                if (hitDistance < 0 || hitDistance > distance)
                    continue;

                var hitPosition = worldRay.GetPoint(hitDistance);

                var r = hitPosition - v0;

                var edge0 = v2 - v0;
                var edge1 = v1 - v0;

                var dot00 = Vector3.Dot(edge0, edge0);
                var dot01 = Vector3.Dot(edge0, edge1);
                var dot11 = Vector3.Dot(edge1, edge1);

                var coeff = 1f / (dot00 * dot11 - dot01 * dot01);

                var dot02 = Vector3.Dot(edge0, r);
                var dot12 = Vector3.Dot(edge1, r);

                var u = coeff * (dot11 * dot02 - dot01 * dot12);
                var v = coeff * (dot00 * dot12 - dot01 * dot02);

                if (u >= 0 && v >= 0 && u + v < 1)
                {
                    found = true;
                    distance = hitDistance;
                    position = hitPosition;
                    normal = plane.normal * normalScale;
                    vertex0 = v0;
                    vertex1 = v1;
                    vertex2 = v2;
                    index0 = i0;
                    index1 = i1;
                    index2 = i2;
                    normal0 = normals[i0];
                    normal1 = normals[i1];
                    normal2 = normals[i2];
                }
            }
            return found;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            var intersection = (MeshIntersection) obj;
            return gameObject == intersection.gameObject &&
                   index0 == intersection.index0 &&
                   index1 == intersection.index1 &&
                   index2 == intersection.index2;
        }

        public override int GetHashCode()
        {
            return index0 + index1 + index2;
        }
    }

}