using System.Collections.Generic;
using UnityEngine;

namespace Ameye.SurfaceIdMapper.Editor.Utilities
{
	public static class Helper  {
		
		// Stupid serialisation hack, since Unity won't serialise a List of Lists
		[System.Serializable]
		private class IntList : List<int> {}
		[SerializeField][HideInInspector]
		private static List<IntList> __islands;

		/// <summary>
		/// List of islands, where an island is a list of triangles
		/// (indices into triangle array - use tris[(listValue*3) + 0,1,2] to access vert)
		/// </summary>
		///
		/// p
		///
		private static List<IntList> GenerateIslands(Mesh mesh)
		{
			var islands = new List<IntList>();
			Vector3[] verts = mesh.vertices;

			// Map [vert index] to [list of duplicate vert indices]
			List<List<int>> vertsToDups = new List<List<int>>();
			for (int i=0; i<verts.Length; ++i){
				List<int> dups = new List<int>();
				vertsToDups.Add(dups);
				for (int j=0; j<verts.Length; ++j){
					if (i==j) continue;
					if (verts[i] == verts[j]){
						dups.Add(j);
					}
				}
			}

			// Rearrange duplicates
			int[] vertsToRemappedVerts = new int[verts.Length];
			List<int>[] remappedVertsToOriginalVerts = new List<int>[verts.Length];
			// Choose lowest-indexed dup for each vert, and remap all references to that
			for (int i=0; i<vertsToDups.Count; ++i){
				int lowest = i;
				List<int> vtd = vertsToDups[i];
				for (int j=0; j<vtd.Count; ++j){
					if (vtd[j] < lowest){
						lowest = vtd[j];
					}
				}
				vertsToRemappedVerts[i] = lowest;
				remappedVertsToOriginalVerts[lowest] = vtd;
			}

			// Generate remapped triangles
			int[] tris = GetTris(mesh);
		
			for (int i=0; i<tris.Length; ++i){
				tris[i] = vertsToRemappedVerts[tris[i]];
			}
			
			// Loop through tris
			for (int i=0; i<tris.Length/3; ++i){
				// Check if this tri has already been mapped
				if (TriAlreadyMapped(islands, i)) continue;

				// An unmapped tri means a new island
				IntList il = new IntList();
				il.Add(i);
				islands.Add(il);

				// Recursively map connected triangles
				MapConnectedTris(islands, il, tris, i);
				
			}
			return islands;
		}
		
		private static Dictionary<int, int> __triToIsland;
		/// <summary>
		/// Map of triangle index of its parent island
		/// </summary>
		private static Dictionary<int, int> _triToIsland {
			get {
				if (__triToIsland == null){
					__triToIsland = new Dictionary<int, int>();

					// Loop through islands
					for (int i=0; i<__islands.Count; ++i){
						// Get all child triangles
						List<int> sm = __islands[i];
						for (int k=0; k<sm.Count; ++k){
							if (__triToIsland.ContainsKey(sm[k])) continue;
							__triToIsland.Add(sm[k], i);
						}
					}
				}
				return __triToIsland;
			}
		}
		/// <summary>
		/// Have islands been mapped yet? Mapping islands can be slow.
		/// </summary>
		public static bool islandsMapped {
			get {
				return __islands != null;
			}
		}
		private static bool TriAlreadyMapped(List<IntList> islands, int t){
			if (islands == null || islands.Count == 0) return false;

			for (int s=0; s<islands.Count; ++s){
				if (islands[s].Contains(t))	return true;
			}

			return false;
		}
		private static void MapConnectedTris(List<IntList> islands, List<int> island, int[] tris, int tri)
		{
			
			int v0 = tris[tri*3];
			int v1 = tris[(tri*3)+1];
			int v2 = tris[(tri*3)+2];
			//Debug.Log("MAPPING FOR TRIANGLE: (" + v0 + ", " + v1 + ", " + v2 + ")");
			
			// Loop through other tris
			for (int j=0; j<tris.Length/3; ++j){
				// Ignore same tri
				if (j == tri) continue;
				// Check if this tri has already been mapped
				if (TriAlreadyMapped(islands, j)) continue;

				var a = new[] {v0, v1, v2};
				var b = new[] {tris[(j * 3) + 0], tris[(j * 3) + 1], tris[(j * 3) + 2]};

				if (AreTrianglesConnected(a, b)) ;
				{
					island.Add(j);
					MapConnectedTris(islands, island, tris, j);
				}

				// See if tri shares any verts with current tri
			/*	for (int k=0; k<3; ++k){
					int v = tris[(j*3)+k];

					/*Debug.Log("v: " + v);
					Debug.Log("v0: " + v0);
					Debug.Log("v1: " + v1);
					Debug.Log("v2: " + v2);
					if (v == v0 || v == v1 || v == v2){
						// Connected - add and recurse
						island.Add(j);
						MapConnectedTris(islands, island, tris, j);
						// Don't break - vert may be shared by more than 2 tris!
					}
					
					
				}*/
			}
		}
		
		private static bool AreTrianglesConnected(int[] a, int[] b)
		{
			// Loop through vertices.
			for (var i = 0; i < 3; i++)
			for (var j = 0; j < 3; j++)
			{
				if (a[i] == b[j])
				{
					Debug.Log("Triangle (" + a[0] + ", " + a[1] + ", " + a[2]  +") is connected to (" + b[0] + ", " + b[1] + ", " + b[2] + ")");
Debug.Log("Because: "  +a[i] + " == " + b[j]);
					return true;
				}
			}
			return false;
		}
		
		public static int[] GetTris(Mesh mesh){
			return mesh.triangles;
		}
		/// <summary>
		/// Returns a list of indices of triangles connected to specified triangle
		/// Argument and returned values used as tris[(value*3) + 0,1,2]
		/// </summary>
		public static List<int> GetConnectedTriangles(Mesh mesh, int triIndex)
		{
			
			__islands = null;
			__islands = GenerateIslands(mesh);
			Debug.Log("mapped islands: " + __islands.Count);
			int ilIndex = -1;
			if (_triToIsland.TryGetValue(triIndex, out ilIndex)){
				if (ilIndex < __islands.Count){
					return __islands[ilIndex];
				}
			}
			return null;
		}
	}
}
