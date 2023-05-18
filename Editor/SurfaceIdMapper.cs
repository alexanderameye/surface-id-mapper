using System;
using System.Collections.Generic;
using System.Linq;
using Ameye.OutlinesToolkit.Editor.Sectioning.Enums;
using Ameye.OutlinesToolkit.Editor.Sectioning.Utilities;
using Ameye.SRPUtilities.Editor.DebugViewer;
using Ameye.SRPUtilities.Editor.Enums;
using Ameye.SRPUtilities.Editor.Utilities;
using Ameye.SurfaceIdMapper.Editor.Marker;
using Ameye.SurfaceIdMapper.Editor.Utilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ameye.SurfaceIdMapper.Editor
{
    public static class SurfaceIdMapper
    {
        private static bool _active;
        private static MeshRenderer _selectedMeshRenderer;
        private static MeshFilter _selectedMeshFilter;
        private static int[] _selectedMeshRendererInstanceIds;

        // dragging
        private static Vector2 _dragStartPosition;
        private static Dictionary<MeshIntersection, bool> _intersectedTrianglesDuringDrag;
        private static readonly List<int> AddedTrianglesDuringDrag = new();
        private static readonly List<Vector3> DragPoints = new();

        // intersection
        private static Ray _intersectionRay;
        private static Mesh _mesh;
        private static Matrix4x4 _meshMatrix;
        private static MeshIntersection _meshIntersection;

        // vertex colors
        private static List<Color32> _vertexColors;
        public static Color32 _pickedColor;
        private static Channel _activeChannel = Channel.R;

        // debug label
        private static Texture2D _debugLabelTexture;
        private static GUIStyle _debugLabelStyle;
        private static GUIContent _debugLabelGuiContent;

        // instruction label
        private static GUIStyle _instructionLabelStyle;
        private static GUIContent _instructionLabelGuiContent;

        // selection
        public static GameObject SelectedGameObject { get; private set; }
        public static Color32 PickedColor => _pickedColor;

        // Actions.
        public static event Action<bool> ToolActiveStatusChanged = delegate { };
        public static event Action<Channel> ActiveChannelChanged = delegate { };
        public static event Action<Color32> ColorPicked = delegate { };
        public static event Action<bool> EnabledStatusChanged = delegate {  };

        
        public static Color MouseIndicatorColor = new(1.0f, 1.0f, 1.0f, 0.5f);
        
        private static List<int> _connectedTriangles = new List<int>();
        
        // Painting.
        private static FillMode _fillMode = FillMode.Greedy;

        public static void SetEnabled(bool enabled)
        {
            EnabledStatusChanged.Invoke(enabled);
        }
        
        public static bool IsActive()
        {
            return _active;
        }

        public static void Enter()
        {
            _active = true;
            ToolActiveStatusChanged(true);

            // Enable surface id mapper debug view.
            DebugViewHandler.EnableDebugView(true);


            var debugViewsAsset = DebugViewHandler.DebugViewsAsset;
            if (debugViewsAsset == null || debugViewsAsset.debugViews == null)
            {
                Debug.LogError("No debug views asset assigned.");
                return;
            }
            DebugViewHandler.SetDebugView(debugViewsAsset.debugViews.Find(view => view.name == "Surface ID Mapper"));

            // scene view
            SceneView.lastActiveSceneView.sceneViewState.SetAllEnabled(false);
            SceneView.RepaintAll();

            // Focus on selected gameobject.
            SceneView.lastActiveSceneView.FrameSelected();

            // register the selected gameobject (that is being painted)
            // note: we can access the MeshRenderer and MeshFilter components because they are guaranteed to be available
            //       because otherwise the section painter tool wasn't enabled but this is a bit hacky idk
            SelectedGameObject = Selection.activeGameObject;
           
            _selectedMeshRenderer = SelectedGameObject.GetComponent<MeshRenderer>();
            _selectedMeshFilter = SelectedGameObject.GetComponent<MeshFilter>();
            _selectedMeshRendererInstanceIds = new[] {_selectedMeshRenderer.GetInstanceID()};
            _mesh = _selectedMeshFilter.sharedMesh;
            
            // Add surface ID map data.
            var stream = SurfaceIdMapperUtility.GetOrAddAdditionalVertexStream(SelectedGameObject);
            if (stream.MeshRenderer.additionalVertexStreams == null)
            {
                stream.OnMeshChanged();
            }
    
            // deselect everything
           // Selection.objects = Array.Empty<Object>();

            #region events

            SceneView.duringSceneGui += OnDuringSceneGui; // important: this is done AFTER _selectedGameObject has been set
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            Undo.undoRedoPerformed += OnUndoRedo;

            #endregion

            // show scene view overlay
            SurfaceIdMapperOverlay.Show();
        }

        public static void Leave()
        {
            // scene view
            SceneView.lastActiveSceneView.sceneViewState.SetAllEnabled(true);
            SceneView.RepaintAll();

            // ui
            Tools.hidden = false; // fixme: idk what this does really, nothing it seems?
            SurfaceIdMapperOverlay.Hide(); // hide scene view overlay
            DebugViewHandler.EnableDebugView(false); // disable sectioning debug view

            // finish editing, need to save the data
            // var paintData = _selectedGameObject.GetComponent<SectionPaintData>();
            // if (paintData) paintData.ApplyVertexColors();
            // AssetDatabase.SaveAssets();
            // AssetDatabase.Refresh();

            _active = false;
            ToolActiveStatusChanged(false);

            // reset the selected gameobject
            Selection.activeGameObject = SelectedGameObject;

            #region events

            SceneView.duringSceneGui -= OnDuringSceneGui;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            Undo.undoRedoPerformed -= OnUndoRedo;

            #endregion

            // cleanup
            //PickColor(Color.black);

        }

        private static void OnUndoRedo()
        {
            if (_selectedMeshFilter)
            {
                var data = SurfaceIdMapperUtility.GetOrAddAdditionalVertexStream(SelectedGameObject);
                if (data) data.OnUndoRedo();
            }
        }

        private static void OnProjectChanged()
        {
            // Leave section marker.
            Leave();
            
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            // Leave section marker.
            Leave();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            // Leave section marker.
            Leave();
        }

        private static void OnDuringSceneGui(SceneView sceneView)
        {
            var currentEvent = Event.current;
            var mousePosition = currentEvent.mousePosition;

            // Prevent the selection of other gameobjects while editing the section marker data.
            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (currentEvent.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);
            var eventType = currentEvent.GetTypeForControl(controlId);

            // scene view drawing (while alt/option key is possible pressed)
            if (currentEvent.type == EventType.Repaint)
            {
                Handles.DrawOutline(_selectedMeshRendererInstanceIds, Color.white);
                //DrawDebugLabel($"SECTIONING EDITOR ({SelectedGameObject.name})");
                DrawInstructionLabel();

                // color picker cursor
                if (currentEvent.shift)
                {
                    // todo: eye dropper cursor? needs custom icon
                    EditorGUIUtility.AddCursorRect(SceneView.lastActiveSceneView.position, MouseCursor.Link);
                    MouseIndicatorColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
                }
            }

            // prevent scene-view panning with right-click
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1)
            {
                Event.current.Use();
            }

            // Stop processing things when alt/option key is pressed (during scene camera movement).
            if (currentEvent.alt) return;

            // Handle events.
            switch (eventType)
            {
                // key pressed
                case EventType.KeyDown:
                    switch (currentEvent.keyCode)
                    {
                        case KeyCode.Escape:
                            Event.current.Use();
                            Leave();
                            break;
                        case KeyCode.F:
                            SetSectionMarkerDataForSelectedGameObject(_pickedColor);
                            break;
                        case KeyCode.R:
                            SetSectionMarkerDataForSelectedGameobject(SectionMarkMode.Random);
                            break;
                        case KeyCode.S:
                            SetSectionMarkerDataForSelectedGameobject(SectionMarkMode.Sequential);
                            break;
                    }
                    break;
                // start dragging
                case EventType.MouseDown:
                    _dragStartPosition = mousePosition;
                    break;
                // while dragging
                case EventType.MouseDrag:
                    // whenever the mouse has move above a certain threshold note: separate threshold for drag points
                    if ((mousePosition - _dragStartPosition).magnitude > 2.0f)
                    {

                        // add the drag position to the list of drag points
                        var position = SceneViewUtilities.GetWorldSpaceHitPositionFromScreenSpacePosition(sceneView.camera, mousePosition);

                        if (position != Vector3.zero) DragPoints.Add(position);
                    }

                    // whenever the mouse has moved above a certain threshold
                    if ((mousePosition - _dragStartPosition).magnitude > 2.0f)
                    {
                        // register the new drag start position
                        _dragStartPosition = mousePosition;

                        // intersection test (while dragging, check if still hovering over the same mesh filter)
                        _intersectionRay = HandleUtility.GUIPointToWorldRay(mousePosition);
                        _meshIntersection.Raycast(_intersectionRay, _selectedMeshFilter);

                        // note: alternatively could complete the paint when we leave and clear the drag
                        if (!_meshIntersection.found) break;

                        // check if we intersect a new triangle that hasn't been intersected before during this drag
                        // fixme: a new triangle could have been intersected that belongs
                        //        to the same 'all connected triangles' collection, which is useless work!
                        //        fix this by adding 'dummy intersections' for these triangles?
                        _intersectedTrianglesDuringDrag ??= new Dictionary<MeshIntersection, bool>();

                        if (!_intersectedTrianglesDuringDrag.ContainsKey(_meshIntersection))
                        {
                            // keep track of the intersection
                            //_intersectedTrianglesDuringDrag.Add(_meshIntersection, true);
                            //Debug.Log(_meshIntersection.index0);
                            _connectedTriangles = GetConnectedTrianglesForMeshIntersection(_meshIntersection, _fillMode);
                            for (var i = 0; i < _connectedTriangles.Count; i += 3)
                            {
                                var intersection = new MeshIntersection
                                {
                                    index0 = _connectedTriangles[i],
                                    index1 = _connectedTriangles[i + 1],
                                    index2 = _connectedTriangles[i + 2],
                                    gameObject = SelectedGameObject
                                };

                                // fixme: there is an issue with this, idk what...
                                if (!_intersectedTrianglesDuringDrag.TryAdd(intersection, true))
                                {
                                    //Debug.Log(intersection.index0);
                                }
                            }
                            AddedTrianglesDuringDrag.AddRange(_connectedTriangles);
                        }
                    }
                    break;
                // stop dragging
                case EventType.MouseUp:
                    // clear the drag points
                    DragPoints.Clear();

                    // if _intersectedTrianglesDuringDrag is empty, that means we just clicked on a single spot
                    if (_intersectedTrianglesDuringDrag == null || _intersectedTrianglesDuringDrag.Count == 0)
                    {
                        // Perform intersection test (check if mouse was clicked over the selected mesh filter).
                        _intersectionRay = HandleUtility.GUIPointToWorldRay(mousePosition);
                        _meshIntersection.Raycast(_intersectionRay, _selectedMeshFilter);
                        if (!_meshIntersection.found) break;

                        // Get all connected triangles for this intersection.
                        // WARN: Expensive function.
                        _connectedTriangles = GetConnectedTrianglesForMeshIntersection(_meshIntersection, _fillMode);
    
                        switch (currentEvent.button)
                        {
                            // right click -> paint random color
                            case 1:
                                AssignRandomSectionColorToTriangles(_connectedTriangles);
                                break;
                            // shift + left click -> pick color
                            case 0 when currentEvent.shift:
                                var paintData = SurfaceIdMapperUtility.GetOrAddAdditionalVertexStream(SelectedGameObject);
                                var colors = paintData.Colors;
                                PickColor(colors[_meshIntersection.index0]);
                                break;
                            // left click -> paint picked color
                            // FIXME: what if _pickedColor is not set?
                            case 0:
                                SetVertexColor(_connectedTriangles, _pickedColor);
                                break;
                        }
                    }

                    // ended a drag with multiple triangles dragged over
                    else
                    {
                        // set the vertex color for all of the dragged over triangles
                        switch (currentEvent.button)
                        {
                            // right click -> paint random color
                            case 1:
                                AssignRandomSectionColorToTriangles(AddedTrianglesDuringDrag);
                                break;
                            // left click -> paint picked color
                            case 0:
                                SetVertexColor(AddedTrianglesDuringDrag, _pickedColor);
                                break;
                        }

                        // reset drag action
                        _intersectedTrianglesDuringDrag.Clear();
                        AddedTrianglesDuringDrag.Clear();
                    }
                    break;
                // mouse move
                case EventType.MouseMove:
                    // intersection test to continuously update hit indicator
                    _intersectionRay = HandleUtility.GUIPointToWorldRay(mousePosition);
                    _meshIntersection.Raycast(_intersectionRay, _selectedMeshFilter);
                    SceneView.RepaintAll();
                    break;
                // scene view drawing (while alt/option key is not pressed)
                case EventType.Repaint:
                    OnSceneViewRepaint(sceneView);
                    break;
            }
        }

        public static void SetSectionMarkerDataForSelectedGameobject(SectionMarkMode application)
        {
            var markerData = SurfaceIdMapperUtility.GetOrAddAdditionalVertexStream(SelectedGameObject);
            Undo.RecordObject(markerData, "Modified Section Marker Data.");
            SurfaceIdMapperUtility.SetSectionMarkerDataForMesh(markerData, _selectedMeshFilter.sharedMesh, _activeChannel, application);
        }

        public static void SetSectionMarkerDataForSelectedGameObject(Color32 color)
        {
            var markerData = SurfaceIdMapperUtility.GetOrAddAdditionalVertexStream(SelectedGameObject);
            Undo.RecordObject(markerData, "Modified Section Marker Data.");
            SurfaceIdMapperUtility.FillMarkerDataWithColor(markerData, _selectedMeshFilter.sharedMesh, _activeChannel, color);
            
        }
        
        /// <summary>
        /// Assign a random color to the section marker data for a given list of triangles.
        /// </summary>
        /// <param name="triangles"></param>
        private static void AssignRandomSectionColorToTriangles(List<int> triangles)
        {
            // get vertex colors
            // FIXME: don't use GetColors, we store colors in paint data... right??
            if (_vertexColors == null) _vertexColors = new List<Color32>(new Color32[_mesh.vertexCount]);
            _mesh.GetColors(_vertexColors);

            // If no vertex colors were set, start from blank list.
            if (_vertexColors.Count == 0) _vertexColors = new List<Color32>(new Color32[_mesh.vertexCount]);

            // Generate a random color.
            var randomColor = SurfaceIdMapperUtility.GetRandomColorForChannel(_activeChannel);

            // Set section marker data.
            SetSectionMarkerDataForTriangles(triangles, randomColor);

            // set the picked color to be the randomly assigned one
            PickColor(randomColor);
        }

        public static void PickColor(Color32 color)
        {
            _pickedColor = color;
            ColorPicked(_pickedColor);
        }

        private static void SetVertexColor(List<int> connectedTriangles, Color32 color)
        {
            Debug.Log("SetVertexColor");

            // _pickedColor = color;

            // get vertex colors
            // FIXME: don't use GetColors, we store colors in paint data...
            _vertexColors ??= new List<Color32>(new Color32[_mesh.vertexCount]);
            _mesh.GetColors(_vertexColors);

            // if no vertex colors were set, start from blank list
            if (_vertexColors.Count == 0)
            {

                Debug.Log("No vertex colors have been set to paint with.");
                _vertexColors = new List<Color32>(new Color32[_mesh.vertexCount]);
            }

            // set paint data
            SetSectionMarkerDataForTriangles(connectedTriangles, color);
        }

        
        
        public static void ClearPaintDataForSelectedGameObject()
        {
            var paintData = SurfaceIdMapperUtility.GetOrAddAdditionalVertexStream(SelectedGameObject);
            Undo.RecordObject(paintData, "Modified SectionPaintData.");

            paintData.SetColor(Color.black);
        }

        
        public static float Frac(float value)
        {
            return (float) (value-Math.Truncate(value));
        }
        
        public static Color HSVToRGB(Color color)
        {
            var K = new Vector4(1.0f, 2.0f / 3.0f, 1.0f / 3.0f, 3.0f);

            var x = Mathf.Abs(Frac(color.r + K.x) * 6.0f - K.w);
            var y = Mathf.Abs(Frac(color.r + K.y) * 6.0f - K.w);
            var z = Mathf.Abs(Frac(color.r + K.z) * 6.0f - K.w);

            var r = color.b * Mathf.Lerp(K.x, Mathf.Clamp01(x - K.x), color.g);
            var g = color.b * Mathf.Lerp(K.x, Mathf.Clamp01(y - K.x), color.g);
            var b = color.b * Mathf.Lerp(K.x, Mathf.Clamp01(z - K.x), color.g);
            return new Color(r, g, b, 1.0f);
        }


     
        /// <summary>
        /// Set the section marker data for a given list of triangles to a given color.
        /// </summary>
        /// <param name="triangles"></param>
        /// <param name="color"></param>
        private static void SetSectionMarkerDataForTriangles(List<int> triangles, Color32 color)
        {
            var data = SurfaceIdMapperUtility.GetOrAddAdditionalVertexStream(SelectedGameObject);
            Undo.RecordObject(data, "Modified SectionPaintData.");

            var colors = data.Colors;
            foreach (var triangle in triangles) SurfaceIdMapperUtility.ModifyColorForChannel(ref colors[triangle], color, _activeChannel);
            data.SetColors(colors);
        }

        private static void OnSceneViewRepaint(SceneView sceneView)
        {
            if (!_meshIntersection.found) return;

            // hit transform
            var hitPoint = _meshIntersection.position;
            var hitNormal = _meshIntersection.normal;

            // get constant screen-space handle size
            var handleSize = HandleUtility.GetHandleSize(hitPoint);
            
            // Draw overlay on hovered triangle.
            Handles.color = Color.Lerp(Color.white, Color.clear, 0.5f);
            Handles.DrawAAConvexPolygon(_meshIntersection.vertex0, _meshIntersection.vertex1, _meshIntersection.vertex2);
            
            // FIXME: EXPENSIVE TO DRAW, MAKES THE TOOL SLOW TO GET AN ACCURATE PREVIEW
           /* var connectedTriangles = GetAllConnectedTrianglesForIntersection(_meshIntersection, _fillMode);
            for (var i = 0; i < connectedTriangles.Count; i += 3)
            {
                int[] triangle = {connectedTriangles[i], connectedTriangles[i + 1], connectedTriangles[i + 2]};
                var v00 = _mesh.vertices[triangle[0]];
                var v11 = _mesh.vertices[triangle[1]];
                var v22 = _mesh.vertices[triangle[2]];
                var localToWorldMatrix = _selectedMeshFilter.transform.localToWorldMatrix;
                Handles.DrawAAConvexPolygon(localToWorldMatrix.MultiplyPoint(v00), localToWorldMatrix.MultiplyPoint(v11), localToWorldMatrix.MultiplyPoint(v22));
            }*/
           

         /*   for (var i = 0; i < AddedTrianglesDuringDrag.Count; i += 3)
            {
                int[] triangle =
                {
                    AddedTrianglesDuringDrag[i], AddedTrianglesDuringDrag[i + 1], AddedTrianglesDuringDrag[i + 2]
                };
                var v00 = _mesh.vertices[triangle[0]];
                var v11 = _mesh.vertices[triangle[1]];
                var v22 = _mesh.vertices[triangle[2]];

                var localToWorldMatrix = _selectedMeshFilter.transform.localToWorldMatrix;
                v00 = localToWorldMatrix.MultiplyPoint(v00);
                v11 = localToWorldMatrix.MultiplyPoint(v11);
                v22 = localToWorldMatrix.MultiplyPoint(v22);

                // Draw triangle overlay.
                Handles.color = Color.Lerp(Color.white, Color.clear, 0.5f);
                Handles.DrawAAConvexPolygon(v00, v11, v22);

                // Draw edges.
                Handles.color = Color.white;
                Handles.DrawPolyLine(v00, v11);
                Handles.DrawPolyLine(v11, v22);
                Handles.DrawPolyLine(v22, v00);

                // Draw vertices.
                var forward = sceneView.camera.transform.forward;
                Handles.color = new Color(0.0f, 0.0f, 0.0f, 0.9f);
                Handles.DrawSolidDisc(v00, -forward, HandleUtility.GetHandleSize(v00) * 0.03f);
                Handles.DrawSolidDisc(v11, -forward, HandleUtility.GetHandleSize(v11) * 0.03f);
                Handles.DrawSolidDisc(v22, -forward, HandleUtility.GetHandleSize(v22) * 0.03f);

                Handles.color = new Color(1.0f, 1.0f, 1.0f, 0.5f);
                Handles.DrawSolidDisc(v00, -forward, HandleUtility.GetHandleSize(v00) * 0.02f);
                Handles.DrawSolidDisc(v11, -forward, HandleUtility.GetHandleSize(v11) * 0.02f);
                Handles.DrawSolidDisc(v22, -forward, HandleUtility.GetHandleSize(v22) * 0.02f);
            }*/

            // Draw mouse indicator.
            Handles.color = new Color(0.0f, 0.0f, 0.0f, 0.9f);
            Handles.DrawSolidDisc(hitPoint, hitNormal, handleSize / 8f);
            //var color = (Color) _pickedColor;
            Handles.color = _pickedColor;//HSVToRGB(new Color((color.r) / 3.0f * 360.0f, 0.6f, 0.6f));
            
                
            Handles.DrawSolidDisc(hitPoint, hitNormal, handleSize / 10f);
            Handles.color = new Color(0.0f, 1.0f, 0.0f, 1.0f);
            Handles.DrawLine(hitPoint, hitPoint + hitNormal * handleSize * 0.5f, 2.0f);

            // draw drag poly line
            Handles.color = new Color(0.0f, 1.0f, 0.0f, 1.0f);
            Handles.DrawAAPolyLine(EditorGUIUtility.whiteTexture, HandleUtility.GetHandleSize(hitPoint) * 10.0f, DragPoints.ToArray());
            SceneView.RepaintAll();
        }

        private static void DrawInstructionLabel()
        {
            if (_instructionLabelStyle == null)
            {
                _instructionLabelStyle = new GUIStyle();
                _instructionLabelStyle.normal.textColor = Color.white;
                _instructionLabelStyle.alignment = TextAnchor.MiddleLeft;
            }

            _instructionLabelGuiContent = EditorGUIUtility.TrTextContent("Left click to mark with picked color\n" +
                                                                         "Right click to mark with random color\n" +
                                                                         "Shift + left click to pick color", "");


            SceneViewUtilities.DrawSceneViewLabel(_instructionLabelStyle, _instructionLabelGuiContent, RelativePosition.BottomLeft);
        }

        private static void DrawDebugLabel(string label)
        {
            if (_debugLabelTexture == null)
            {
                _debugLabelTexture = new Texture2D(1, 1);
                _debugLabelTexture.SetPixel(0, 0, Color.Lerp(Color.white, Color.black, 0.88f));
                _debugLabelTexture.Apply();
            }

            if (_debugLabelStyle == null)
            {
                _debugLabelStyle = new GUIStyle();
                _debugLabelStyle.normal.background = _debugLabelTexture;
                _debugLabelStyle.fontStyle = FontStyle.Bold;
                _debugLabelStyle.normal.textColor = new Color(0.96f, 0.765f, 0.27f, 1.0f);
                _debugLabelStyle.alignment = TextAnchor.MiddleCenter;
            }

            _debugLabelGuiContent = EditorGUIUtility.TrTextContent(label, "");

            SceneViewUtilities.DrawSceneViewLabel(_debugLabelStyle, _debugLabelGuiContent, RelativePosition.TopCenter);
        }


        private static List<int> GetConnectedTrianglesForMeshIntersection(MeshIntersection intersection, FillMode fillMode)
        {
            // get reference to the selected gameobject
            var go = intersection.gameObject;
            _mesh = go.GetComponent<MeshFilter>().sharedMesh;
            _meshMatrix = _selectedMeshFilter.transform.localToWorldMatrix;

            // get the picked triangle and all the connected ones
            int[] triangle = {intersection.index0, intersection.index1, intersection.index2};

            var normal = intersection.normal;


            if (fillMode != FillMode.Greedy) return triangle.ToList();
            var triangles = new List<int>(_mesh.triangles);
            var normals = _mesh.normals.ToList();
            
            var data = SurfaceIdMapperUtility.GetOrAddAdditionalVertexStream(SelectedGameObject);
            
            return data.GetConnectedTriangles(triangle);
        }


        public static void SetActiveChannel(Channel channel)
        {
            _activeChannel = channel;
        }

        private static void OnActiveChannelChanged(Channel obj)
        {
            ActiveChannelChanged(obj);
        }

        public static void SetFillMode(FillMode fillMode)
        {
            _fillMode = fillMode;
        }
    }
}