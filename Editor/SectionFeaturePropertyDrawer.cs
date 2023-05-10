using Ameye.OutlinesToolkit.Section;
using Ameye.OutlinesToolkit.Sectioning;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace TheBlackIslandEditor.Rendering.Passes
{
    [CustomPropertyDrawer(typeof(SectionFeature.Settings))]
    public class SectionFeaturePropertyDrawer : PropertyDrawer
    {
        private GUIStyle boldLabel;
        private bool createdStyles;

        private void CreateStyles()
        {
            createdStyles = true;
            boldLabel = GUI.skin.label;
            boldLabel.fontStyle = FontStyle.Bold;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!createdStyles) CreateStyles();

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.indentLevel = 0;
            EditorGUILayout.PropertyField(property.FindPropertyRelative("injectionPoint"),
                EditorGUIUtility.TrTextContent("Injection"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("sectionBufferFormat"),
                EditorGUIUtility.TrTextContent("Buffer Format"));
            //EditorGUILayout.Space();
          //  EditorGUILayout.LabelField("Rendered Objects", EditorStyles.boldLabel);
            //EditorGUILayout.PropertyField(property.FindPropertyRelative("layer"));
            //EditorGUILayout.PropertyField(property.FindPropertyRelative("clearFlag"));
          //  EditorGUILayout.Space();
           // EditorGUILayout.LabelField("Section", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(property.FindPropertyRelative("clearColor"));
            //EditorGUILayout.PropertyField(property.FindPropertyRelative("format"));
            //EditorGUILayout.PropertyField(property.FindPropertyRelative("depthBufferBits"));

            EditorGUI.EndProperty();
            property.serializedObject.ApplyModifiedProperties();
        }
    }
}