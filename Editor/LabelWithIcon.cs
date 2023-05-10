using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ameye.OutlinesToolkit.Editor
{
    public class LabelWithIcon : VisualElement
    {
        private readonly Image iconElement;
        private TextElement textElement;

        public LabelWithIcon(string icon)
        {
            style.flexDirection = FlexDirection.Row;
            iconElement = new Image
            {
                scaleMode = ScaleMode.ScaleToFit
            };
            iconElement.AddToClassList("unity-editor-toolbar-element__icon");
            Icon = EditorGUIUtility.IconContent(icon).image as Texture2D;
            Add(iconElement);
        }

        public string Text
        {
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    textElement?.RemoveFromHierarchy();
                    textElement = null;
                }
                else
                {
                    if (textElement == null)
                    {
                        Insert(IndexOf(iconElement) + 1, textElement = new TextElement());
                        textElement.AddToClassList("unity-editor-toolbar-element__label");
                        textElement.style.marginRight = 2.0f;
                    }
                    textElement.text = value;
                }
            }
        }

        private Texture2D Icon
        {
            set
            {
                iconElement.image = value;
                iconElement.style.display = value != null ? DisplayStyle.Flex : DisplayStyle.None;
                iconElement.style.marginLeft = 2.0f;
                iconElement.style.marginRight = 2.0f;
            }
        }
    }
}