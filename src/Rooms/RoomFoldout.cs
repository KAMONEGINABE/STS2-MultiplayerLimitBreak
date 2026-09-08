using Godot;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Ui.Shell.Theme;

namespace STS2MultiplayerLimitBreak.Rooms
{
    internal sealed partial class RoomFoldout : VBoxContainer
    {
        private readonly ModSettingsGamepadCompatibleButton _toggle;
        private readonly PanelContainer _headerSurface;
        private readonly PanelContainer _bodySurface;
        private readonly Label _arrow;
        private readonly Label _title;
        private bool _hovered;
        private string? _hint;
        public HBoxContainer Header { get; }
        public event Action<bool>? ExpandedChanged;

        public RoomFoldout() : this("", new VBoxContainer(), false) { }

        public RoomFoldout(string title, Control body, bool expanded)
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 0);
            _headerSurface = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            Header = RoomUi.Row();
            _toggle = new()
            {
                Text = "",
                Flat = true,
                CustomMinimumSize = new(160, RitsuShellTheme.Current.Metric.Entry.ValueMinHeight),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.Fill,
                FocusMode = FocusModeEnum.All,
                MouseDefaultCursorShape = CursorShape.PointingHand,
            };
            foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
                _toggle.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            var labels = RoomUi.Row();
            labels.MouseFilter = MouseFilterEnum.Ignore;
            _arrow = RoomUi.Label("", RitsuShellTheme.Current.Metric.FontSize.HeaderArrow, true);
            _arrow.SizeFlagsHorizontal = SizeFlags.Fill;
            _arrow.CustomMinimumSize = new(28, 0);
            _arrow.HorizontalAlignment = HorizontalAlignment.Center;
            _arrow.AutowrapMode = TextServer.AutowrapMode.Off;
            _arrow.AddThemeColorOverride("font_color", RitsuShellTheme.Current.Text.RichSecondary);
            labels.AddChild(_arrow);
            _title = RoomUi.Label(title, RitsuShellTheme.Current.Metric.FontSize.HeaderTitle, true);
            _title.HorizontalAlignment = HorizontalAlignment.Left;
            _title.AutowrapMode = TextServer.AutowrapMode.Off;
            _title.ClipText = true;
            _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            labels.AddChild(_title);
            _toggle.AddChild(labels);
            labels.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            Header.AddChild(_toggle);
            _headerSurface.AddChild(Header);
            AddChild(_headerSurface);
            _bodySurface = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = expanded };
            var bodyStyle = (StyleBoxFlat)ModSettingsUiFactory.CreateSurfaceStyle().Duplicate();
            bodyStyle.ContentMarginLeft = bodyStyle.ContentMarginRight = 12;
            bodyStyle.ContentMarginTop = bodyStyle.ContentMarginBottom = 8;
            _bodySurface.AddThemeStyleboxOverride("panel", bodyStyle);
            _bodySurface.AddChild(body);
            AddChild(_bodySurface);
            _toggle.Pressed += () => SetExpanded(!_bodySurface.Visible);
            _toggle.MouseEntered += () => { _hovered = true; UpdateHeader(); };
            _toggle.MouseExited += () => { _hovered = false; UpdateHeader(); };
            _toggle.FocusEntered += UpdateHeader;
            _toggle.FocusExited += UpdateHeader;
            SetTitle(title);
            UpdateHeader();
        }

        public void SetTitle(string title)
        {
            _title.Text = title;
            _toggle.TooltipText = _hint ?? title;
        }

        public void SetHint(string hint)
        {
            _hint = hint;
            _toggle.TooltipText = hint;
        }

        private void SetExpanded(bool expanded)
        {
            _bodySurface.Visible = expanded;
            UpdateHeader();
            ExpandedChanged?.Invoke(expanded);
        }

        private void UpdateHeader()
        {
            var theme = RitsuShellTheme.Current;
            var selected = _bodySurface.Visible;
            var state = selected ? theme.Component.Collapsible.Selected
                : _hovered || _toggle.HasFocus() ? theme.Component.Collapsible.Hover : theme.Component.Collapsible.Default;
            var border = Metric("components.collapsible.layout.borderWidth", 2);
            var radius = Metric("components.collapsible.layout.cornerRadius", theme.Metric.Radius.Default);
            _headerSurface.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = state.Bg,
                BorderColor = _toggle.HasFocus() ? theme.Text.HoverHighlight : state.Border,
                BorderWidthLeft = border,
                BorderWidthRight = border,
                BorderWidthTop = border,
                BorderWidthBottom = border,
                CornerRadiusTopLeft = radius,
                CornerRadiusTopRight = radius,
                CornerRadiusBottomLeft = radius,
                CornerRadiusBottomRight = radius,
                ContentMarginLeft = 10,
                ContentMarginRight = 10,
                ContentMarginTop = 4,
                ContentMarginBottom = 4,
            });
            _arrow.Text = selected ? "▼" : "▶";
        }

        private static int Metric(string key, int fallback) =>
            RitsuShellTheme.Current.TryGetNumber(key, out var value) ? (int)value : fallback;
    }
}
