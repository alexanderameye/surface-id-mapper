using UnityEditor;
using UnityEngine;

namespace Ameye.SurfaceIdMapper.Editor.Utilities
{
    public enum RelativePosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
    
    public static class SceneViewUtilities
    {
        public static Vector3 GetWorldSpaceHitPositionFromScreenSpacePosition(Camera camera, Vector2 mousePosition)
        {
            var ppp = EditorGUIUtility.pixelsPerPoint;
            mousePosition.y = camera.pixelHeight - mousePosition.y * ppp;
            mousePosition.x *= ppp;

            var ray = camera.ScreenPointToRay(mousePosition);
            return Physics.Raycast(ray, out var hit) ? hit.point : Vector3.zero;
        }

        public static void DrawSceneViewLabel(GUIStyle style, GUIContent content, RelativePosition relativePosition)
        {
            var currentCamera = Camera.current;
            var padding = new Vector2(15.0f, 10.0f);

            var size = style.CalcSize(content);
            var origin = Vector2.zero;

            switch (relativePosition)
            {
                // note: origin is measured from top-left
                case RelativePosition.TopCenter:
                    origin = new Vector2(currentCamera.pixelWidth * 0.25f, 0.0f)
                             + new Vector2(-size.x * 0.5f, 10.0f);
                    break;
                case RelativePosition.BottomRight:
                    origin = new Vector2(currentCamera.pixelWidth * 0.5f, currentCamera.pixelHeight * 0.5f)
                             - size
                             + new Vector2(-30.0f, -20.0f);
                    break;
                case RelativePosition.BottomLeft:
                    origin = new Vector2(0.0f, currentCamera.pixelHeight * 0.5f - size.y)
                             + new Vector2(10.0f, -15.0f);
                    break;
                case RelativePosition.TopLeft:
                    origin = new Vector2(0.0f, 0.0f);
                    break;
            }
            var rect = new Rect(origin, size);
            rect.width += padding.x;
            rect.height += padding.y;

            Handles.BeginGUI();
            GUI.Label(rect, content.text, style);
            Handles.EndGUI();
        }
        
        
    }
}