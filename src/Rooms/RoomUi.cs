using Godot;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Platform;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Ui.Shell.Theme;

namespace STS2MultiplayerLimitBreak.Rooms
{
    internal static class RoomUi
    {
        public const float ButtonWidth = 112;
        public const float ActionButtonWidth = 208;
        public static TextureButton SettingsButton(Action action)
        {
            var button = new TextureButton
            {
                TextureNormal = ResourceLoader.Load<Texture2D>("res://images/atlases/ui_atlas.sprites/top_bar/top_bar_settings.tres"),
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
                TooltipText = RoomService.L("rooms.settings", "Room settings"),
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                FocusMode = Control.FocusModeEnum.All,
                SelfModulate = new(0.8f, 0.8f, 0.8f),
            };
            var hovered = false;
            button.MouseEntered += () => { hovered = true; UpdateHighlight(); };
            button.MouseExited += () => { hovered = false; UpdateHighlight(); };
            button.FocusEntered += UpdateHighlight;
            button.FocusExited += UpdateHighlight;
            button.Pressed += action;
            return button;

            void UpdateHighlight()
            {
                button.SelfModulate = hovered || button.HasFocus() ? Colors.White : new(0.8f, 0.8f, 0.8f);
            }
        }

        public static ModSettingsTextButton Button(string text, Action action, ModSettingsButtonTone tone = ModSettingsButtonTone.Normal,
            float width = ButtonWidth, string? hint = null)
        {
            return new ModSettingsTextButton(text, tone, action)
            {
                CustomMinimumSize = new(width, RitsuShellTheme.Current.Metric.Entry.ValueMinHeight),
                ClipText = true,
                TooltipText = hint ?? "",
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            };
        }

        public static void Hint(Control control, string text)
        {
            control.TooltipText = text;
            if (control is Label) control.MouseFilter = Control.MouseFilterEnum.Stop;
            foreach (var child in control.GetChildren().OfType<Control>())
                if (string.IsNullOrEmpty(child.TooltipText)) Hint(child, text);
        }

        public static ScrollContainer Scroll(Control content)
        {
            var scroll = new ScrollContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                ClipContents = true,
            };
            ModSettingsUiControlTheming.ApplySettingsScrollContainerTheme(scroll);
            var frame = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            frame.AddThemeConstantOverride("margin_left", 4);
            frame.AddThemeConstantOverride("margin_top", 4);
            frame.AddThemeConstantOverride("margin_bottom", 4);
            frame.AddChild(content);
            scroll.AddChild(frame);
            scroll.Resized += UpdateGutter;
            scroll.GetVScrollBar().VisibilityChanged += UpdateGutter;
            UpdateGutter();
            return scroll;

            void UpdateGutter()
            {
                frame.AddThemeConstantOverride("margin_right",
                    ModSettingsUiControlTheming.ResolveSettingsScrollContentRightGutter(scroll) + 8);
            }
        }

        public static LineEdit Input(int maxLength, float minWidth)
        {
            var edit = new LineEdit
            {
                MaxLength = maxLength,
                ContextMenuEnabled = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new(minWidth, RitsuShellTheme.Current.Metric.Entry.ValueMinHeight),
            };
            ModSettingsUiControlTheming.ApplyEntryLineEditValueFieldTheme(
                edit, RitsuShellTheme.Current.Font.Body, RitsuShellTheme.Current.Metric.FontSize.Button);
            return edit;
        }

        public static HBoxContainer Toggle(string text, bool value, Action<bool> changed, out ModSettingsToggleControl toggle)
        {
            var row = Row();
            row.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            var label = Label(text);
            label.AutowrapMode = TextServer.AutowrapMode.Off;
            row.AddChild(label);
            toggle = new(value, changed)
            {
                CustomMinimumSize = new(72, RitsuShellTheme.Current.Metric.Entry.ValueMinHeight),
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            };
            row.AddChild(toggle);
            return row;
        }

        public static HFlowContainer Actions()
        {
            var flow = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            flow.AddThemeConstantOverride("h_separation", 8);
            flow.AddThemeConstantOverride("v_separation", 8);
            return flow;
        }

        public static Label Label(string text, int? fontSize = null, bool heading = false)
        {
            var label = new Label
            {
                Text = text,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeFontOverride("font", heading ? RitsuShellTheme.Current.Font.BodyBold : RitsuShellTheme.Current.Font.Body);
            label.AddThemeFontSizeOverride("font_size", fontSize ?? RitsuShellTheme.Current.Metric.FontSize.SettingLineTitle);
            label.AddThemeColorOverride("font_color", heading ? RitsuShellTheme.Current.Text.RichTitle : RitsuShellTheme.Current.Text.RichBody);
            label.VerticalAlignment = VerticalAlignment.Center;
            return label;
        }

        public static HBoxContainer Row()
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 8);
            return row;
        }

        public static HBoxContainer Field(string title, Control editor, string? tooltip = null)
        {
            var row = Row();
            row.AddThemeConstantOverride("separation", 10);
            var label = Label(title, RitsuShellTheme.Current.Metric.FontSize.SettingLineTitle, true);
            label.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            label.CustomMinimumSize = new(148, 0);
            label.TooltipText = tooltip ?? "";
            label.MouseFilter = Control.MouseFilterEnum.Stop;
            if (!string.IsNullOrEmpty(tooltip)) Hint(editor, tooltip);
            row.AddChild(label);
            editor.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            editor.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(editor);
            return row;
        }

        public static void Clear(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                node.RemoveChild(child);
                child.QueueFree();
            }
        }

        public static void AttachSettingsButton(Control parent, INetGameService session)
        {
            if (!RoomService.Available || session.Platform != PlatformType.Steam || session.Type == NetGameType.Singleplayer) return;
            const string name = "MlbRoomSettings";
            var previous = parent.GetNodeOrNull<Control>(name);
            if (previous != null)
            {
                parent.RemoveChild(previous);
                previous.QueueFree();
            }
            var button = SettingsButton(() =>
            {
                RoomService.Bind(session);
                RoomService.OpenSettings();
            });
            button.Name = name;
            button.AnchorLeft = 1;
            button.AnchorRight = 1;
            button.OffsetLeft = -34;
            button.OffsetRight = -6;
            button.OffsetTop = -38;
            button.OffsetBottom = -10;
            parent.AddChild(button);
        }
    }

    internal sealed partial class RoomSecretEdit : HBoxContainer
    {
        private readonly LineEdit _edit;
        private readonly Button _reveal;
        private string _value;
        private bool _revealed;
        private bool _changing;
        private string _placeholder = RoomService.L("rooms.codePlaceholder", "Enter or paste code");
        public event Action<string>? ValueChanged;
        public string Value => _value;

        public RoomSecretEdit() : this("", 128) { }

        public RoomSecretEdit(string value, int maxLength)
        {
            _value = value;
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 8);
            _edit = RoomUi.Input(maxLength, 160);
            _edit.Secret = true;
            _edit.FocusEntered += () => UpdateText(true);
            _edit.FocusExited += () => UpdateText(_revealed);
            _edit.TextChanged += text =>
            {
                if (_changing) return;
                _value = text;
                ValueChanged?.Invoke(text);
            };
            AddChild(_edit);
            _reveal = RoomUi.Button(RoomService.L("rooms.show", "Show"), () =>
            {
                _revealed = !_revealed;
                _edit.Secret = !_revealed;
                UpdateText(_revealed || _edit.HasFocus());
                _reveal!.Text = RoomService.L(_revealed ? "rooms.hide" : "rooms.show", _revealed ? "Hide" : "Show");
            }, hint: RoomService.L("rooms.revealHint", "Toggle visible text. Keep hidden while streaming."));
            AddChild(_reveal);
            UpdateText(false);
        }

        private void UpdateText(bool showText)
        {
            _changing = true;
            _edit.Text = showText ? _value : "";
            _edit.PlaceholderText = _value.Length == 0 ? _placeholder : "••••••••";
            _changing = false;
        }

        public void Conceal(bool clear = false)
        {
            _revealed = false;
            _edit.Secret = true;
            if (clear) _value = "";
            UpdateText(false);
            _reveal.Text = RoomService.L("rooms.show", "Show");
        }

        public void FocusInput() => _edit.GrabFocus();

        public void SetPlaceholder(string placeholder)
        {
            _placeholder = placeholder;
            UpdateText(_revealed || _edit.HasFocus());
        }

        public override void _Notification(int what)
        {
            if (what == NotificationVisibilityChanged && !IsVisibleInTree() && _edit != null && _reveal != null)
                Conceal();
        }

        public override void _ExitTree() => Conceal();
    }
}
