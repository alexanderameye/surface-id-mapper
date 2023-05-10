using System.Collections.Generic;
using Ameye.SRPUtilities.Editor.DebugViewer;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace Ameye.OutlinesToolkit.Editor.Sectioning.Painter
{
    [Overlay(typeof(SceneView), "Section Painter")]
    public class SectionPainterOverlay : Overlay, ITransientOverlay
    {
        // UXML names
        //private const string RadiusSliderName = "radius-slider";
        //private const string DilationToggleName = "dilation-toggle";
        private const string PaintMaskName = "paint-mask";
        private const string ClearAllButtonName = "clear-all-button";
        private const string ClearPaintButtonName = "clear-paint-button";
        private const string ClearMaskButtonName = "clear-mask-button";
        //private const string ChannelDropdownFieldName = "channel-dropdown-field";

        private static VisualElement _paintMask;
        private static Slider _radiusSlider;
        private static Toggle _dilationToggle;
        private static Button _clearAllButton, _clearPaintButton, _clearMaskButton;
        private static DropdownField _channelDropdownField;

        private Background background;

        private static bool _visible;
        public bool visible => _visible;

        public static void Show()
        {
            _visible = true;
            
        }

        public static void Hide()
        {
            _visible = false;
        }

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            VisualElement content = UIToolkitUtilities.GetVisualTreeAsset("SectionPainterOverlay").Instantiate();
            root.Add(content);

            //_radiusSlider = root.Q<Slider>(RadiusSliderName);
            //_radiusSlider.RegisterValueChangedCallback(evt => SectionPainter.SetBrushRadius(evt.newValue));

            _paintMask = root.Q<VisualElement>(PaintMaskName);
            background = new Background
            {
                renderTexture = SectionPainter._sourcePaintRT
            };
            _paintMask.style.backgroundImage = new StyleBackground(background);

        /*    _dilationToggle = root.Q<Toggle>(DilationToggleName);
            _dilationToggle.RegisterValueChangedCallback(evt => SectionPainter.SetDilation(evt.newValue));
*/
            
            // Buttons.
            _clearAllButton = root.Q<Button>(ClearAllButtonName);
            _clearAllButton.clickable.clicked += SectionPainter.ClearSectionPaint;
            //_clearPaintButton = root.Q<Button>(ClearPaintButtonName);
            //_clearPaintButton.clickable.clicked += SectionPainter.ClearPaint;
            //_clearMaskButton = root.Q<Button>(ClearMaskButtonName);
            //_clearMaskButton.clickable.clicked += SectionPainter.ClearMask;

            // Channel dropdowns.
            // TODO: Do these choices string different... strings as variables not great
            /*_channelDropdownField = root.Q<DropdownField>(ChannelDropdownFieldName);
            _channelDropdownField.choices = new List<string> {"Paint (R)", "Mask (G)"};
            _channelDropdownField.RegisterValueChangedCallback(evt => { SectionPainter.SetTargetChannel(evt.newValue); });
            _channelDropdownField.value = "Paint (R)";
*/
            
            // Initialize to only show R and G channel.
            DebugViewHandler.EnableRChannel(true);
            DebugViewHandler.EnableGChannel(true);
            DebugViewHandler.EnableBChannel(false);

            collapsed = false;
            return root;
        }
    }
}