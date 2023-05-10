using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ameye.OutlinesToolkit.Editor
{
    public static class UIToolkitUtilities
    {
        public static void DisplayVisualElement(VisualElement visualElement, bool show)
        {
            if (visualElement != null) visualElement.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static void EnableVisualElement(VisualElement visualElement, bool enable)
        {
            if (visualElement != null) visualElement.SetEnabled(enable);
        }

        public static Image GetImageWithClasses(string[] classNames)
        {
            var img = new Image();
            foreach (var className in classNames)
            {
                img.AddToClassList(className);
            }
            img.style.alignSelf = Align.Center;
            return img;
        }

        public static Rect GetRect(this VisualElement element)
        {
            return new Rect(element.LocalToWorld(element.contentRect.position), element.contentRect.size);
        }

        public static void RecomputeSize(VisualElement container)
        {
            if (container == null)
                return;

            var parent = container.parent;
            container.RemoveFromHierarchy();
            parent.Add(container);
        }

        public static VisualTreeAsset GetVisualTreeAsset(string name)
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(VisualTreeAsset)} {name}");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}