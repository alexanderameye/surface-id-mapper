using Ameye.OutlinesToolkit.Editor.Sectioning.Enums;
using Ameye.SRPUtilities.Editor.DebugViewer;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ameye.OutlinesToolkit.Editor.Sectioning.Marker
{
    [Overlay(typeof(SceneView), "Section Marker Overlay")]
    public class SectionMarkerOverlay : Overlay, ITransientOverlay
    {
        // UXML names
        private const string ChannelEnumName = "channel-enum";
        private const string FillModeEnumName = "fill-mode-enum";
        private const string ColorFieldName = "color-field";
        private const string FillButtonName = "fill-button";
        private const string ClearButtonName = "clear-button";
        private const string RandomizeButtonName = "randomize-button";
        private const string SetSequentialButtonName = "set-sequential-button";

        private static EnumField _channelEnum, _fillModeEnum;
        private static ColorField _colorField;
        private static Button _fillButton, _clearButton, _randomizeButton, _setSequentialButton;


        private static bool _visible;
        public bool visible => _visible;

        public static void Show()
        {
            _visible = true;

            // subscribe to events
            // Selection.selectionChanged += UpdateContextLabel;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            SectionMarker.ColorPicked += OnColorPicked;
            SectionMarker.ActiveChannelChanged += OnActiveChannelChanged;
        }

        public static void Hide()
        {
            _visible = false;

            // unsubscribe from events
            //Selection.selectionChanged -= UpdateContextLabel;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            SectionMarker.ColorPicked -= OnColorPicked;
            SectionMarker.ActiveChannelChanged -= OnActiveChannelChanged;
        }

        private static void OnHierarchyChanged()
        {
            // update the context label with the newly selected gameobject
            //       UpdateContextLabel();
        }


        // note: this may be called from section painter
        private static void OnActiveChannelChanged(Channel channel)
        {
            DebugViewHandler.EnableRChannel(channel == Channel.R);
            DebugViewHandler.EnableGChannel(channel == Channel.G);
            DebugViewHandler.EnableBChannel(channel == Channel.B);
        }

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            VisualElement content = UIToolkitUtilities.GetVisualTreeAsset("SectionMarkerOverlay").Instantiate();
            root.Add(content);

            _channelEnum = root.Q<EnumField>(ChannelEnumName);
            _channelEnum.Init(Channel.R);
            _channelEnum.RegisterValueChangedCallback(evt =>
            {
                var channel = (Channel) evt.newValue;
                DebugViewHandler.EnableRChannel(channel == Channel.R);
                DebugViewHandler.EnableGChannel(channel == Channel.G);
                DebugViewHandler.EnableBChannel(channel == Channel.B);
                SectionMarker.SetActiveChannel(channel);
            });
            
            _fillModeEnum = root.Q<EnumField>(FillModeEnumName);
            _fillModeEnum.Init(FillMode.Greedy);
            _fillModeEnum.RegisterValueChangedCallback(evt =>
            {
                var fillMode = (FillMode) evt.newValue;
                SectionMarker.SetFillMode(fillMode);
            });

            _colorField = root.Q<ColorField>(ColorFieldName);
            //_colorField.value = SectionMarker._pickedColor;
            _colorField.RegisterValueChangedCallback(evt => { SectionMarker.PickColor(evt.newValue); });

            _fillButton = root.Q<Button>(FillButtonName);
            _fillButton.clicked += OnFillButtonClicked;

            _clearButton = root.Q<Button>(ClearButtonName);
            _clearButton.clicked += OnClearButtonClicked;

            _randomizeButton = root.Q<Button>(RandomizeButtonName);
            _randomizeButton.clicked += OnRandomizeButtonClicked;

            _setSequentialButton = root.Q<Button>(SetSequentialButtonName);
            _setSequentialButton.clicked += OnSetSequentialButtonClicked;


            // initialize to only show red channel (default)
            DebugViewHandler.EnableRChannel(true);
            DebugViewHandler.EnableGChannel(false);
            DebugViewHandler.EnableBChannel(false);
            SectionMarker.SetActiveChannel(Channel.R);

            collapsed = false;

            return root;
        }

        private void OnSetSequentialButtonClicked()
        {
            SectionMarker.SetSectionMarkerDataForSelectedGameobject(SectionMarkMode.Sequential);
        }

        private void OnRandomizeButtonClicked()
        {
            SectionMarker.SetSectionMarkerDataForSelectedGameobject(SectionMarkMode.Random);
        }

        private void OnFillButtonClicked()
        {
            SectionMarker.SetSectionMarkerDataForSelectedGameObject(SectionMarker.PickedColor);
        }

        private void OnClearButtonClicked()
        {
            SectionMarker.ClearPaintDataForSelectedGameObject();
        }

        private static void OnColorPicked(Color32 color)
        {
            if (_colorField != null)
            {
                _colorField.value = color;
            }
        }
    }
}