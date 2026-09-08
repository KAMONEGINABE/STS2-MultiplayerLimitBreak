using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Ui.Shell;
using STS2RitsuLib.Ui.Shell.Theme;
using STS2RitsuLib.Ui.Windows;

namespace STS2MultiplayerLimitBreak.Rooms
{
    internal sealed partial class RoomSettingsPopup : Control
    {
        private static RoomSettingsPopup? _current;
        private CanvasLayer _layer = null!;
        private Control? _previousFocus;
        private RitsuFloatingWindow? _roomWindow;
        private RitsuFloatingWindow? _groupsWindow;
        private RitsuFloatingWindow? _inviteWindow;
        private RoomSecretEdit? _inviteInput;
        private Control? _source;
        private Action<ulong>? _join;
        private Button? _roomBack;
        private Button? _groupsBack;
        private bool _initialGroups;
        private bool _closing;
        private bool _switching;
        private ulong _lobby;
        private bool _runInProgress;

        public static void OpenInvite(Control source, Action<ulong> join) => Open(false, source, join);

        public static bool Open(bool groups, Control? source = null, Action<ulong>? join = null)
        {
            if (!groups && join == null && !RoomService.CanOpenRoomSettings) return false;
            if (_current != null && GodotObject.IsInstanceValid(_current) && !_current._closing)
            {
                if (groups) _current.ShowGroups();
                else _current.ShowRoom();
                return true;
            }
            var game = NGame.Instance;
            if (game == null || !GodotObject.IsInstanceValid(game) || !game.IsInsideTree() ||
                NModalContainer.Instance?.OpenModal != null) return false;

            var layer = new CanvasLayer { Name = "MlbRoomSettingsLayer", Layer = 120 };
            var popup = new RoomSettingsPopup
            {
                Name = "MlbRoomSettingsPopup",
                _layer = layer,
                _initialGroups = groups,
                _source = source,
                _join = join,
                _previousFocus = game.GetViewport().GuiGetFocusOwner(),
                _lobby = RoomService.LobbyId,
                _runInProgress = RunManager.Instance?.IsInProgress == true,
            };
            _current = popup;
            layer.AddChild(popup);
            game.AddChild(layer);
            return true;
        }

        public override void _Ready()
        {
            RitsuShellTooltipTheme.ApplyToTreeRoot(this);
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            var dim = new ColorRect
            {
                Color = RitsuShellTheme.Current.Color.ModalBackdrop,
                MouseFilter = MouseFilterEnum.Stop,
            };
            AddChild(dim);
            dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            GetViewport().GuiFocusChanged += KeepFocusInside;
            if (_join != null) ShowInvite();
            else if (_initialGroups) ShowGroups();
            else ShowRoom();
        }

        private RitsuFloatingWindow CreateWindow(bool groups, out Button back)
        {
            var content = new VBoxContainer();
            content.AddThemeConstantOverride("separation", 12);
            if (groups)
                content.AddChild(new MatchGroupsControl { SizeFlagsVertical = SizeFlags.ExpandFill });
            else
            {
                content.AddChild(RoomUi.Scroll(new RoomSettingsControl()));
            }
            var backToRoom = groups && _roomWindow != null;
            back = RoomUi.Button(
                backToRoom ? RoomService.L("rooms.backToRoom", "Back to room settings") : RoomService.L("rooms.done", "Done"),
                groups ? BackFromGroups : Close, width: RoomUi.ActionButtonWidth);
            var footer = RoomUi.Row();
            footer.Alignment = BoxContainer.AlignmentMode.End;
            footer.AddChild(back);
            content.AddChild(footer);
            var window = new RitsuFloatingWindow(new()
            {
                Title = RoomService.L(groups ? "rooms.groupsTitle" : "rooms.popupTitle", groups ? "[MLB] Match groups" : "[MLB] Room settings"),
                InitialSize = groups ? new(1040, 760) : new(920, 620),
                MinimumSize = groups ? new(860, 500) : new(800, 420),
                FitInitialSizeToContent = false,
                StartCentered = true,
            });
            window.SetContent(content);
            window.Closed += (_, _) => { if (groups) BackFromGroups(); else Close(); };
            AddChild(window);
            return window;
        }

        private void ShowInvite()
        {
            var content = new VBoxContainer();
            content.AddThemeConstantOverride("separation", 12);
            _inviteInput = new("", 256);
            _inviteInput.SetPlaceholder(RoomService.L("rooms.invitePlaceholder", "Paste an invite code or Steam link"));
            content.AddChild(_inviteInput);
            var error = RoomUi.Label("");
            error.Visible = false;
            content.AddChild(error);
            var actions = RoomUi.Row();
            actions.Alignment = BoxContainer.AlignmentMode.End;
            actions.AddChild(RoomUi.Button(RoomService.L("rooms.close", "Close"), Close));
            actions.AddChild(RoomUi.Button(RoomService.L("rooms.join", "Join"), () =>
            {
                if (!RoomInviteCodec.TryDecode(_inviteInput.Value, out var lobby))
                {
                    error.Text = RoomService.L("rooms.invalidInvite", "Invalid invite code or Steam link.");
                    error.Show();
                    return;
                }
                Close();
                _join!(lobby);
            }, ModSettingsButtonTone.Accent));
            content.AddChild(actions);
            _inviteWindow = new(new()
            {
                Title = RoomService.L("rooms.inviteTitle", "[MLB] Join room"),
                InitialSize = new(640, 250),
                MinimumSize = new(600, 230),
                FitInitialSizeToContent = true,
                StartCentered = true,
            });
            _inviteWindow.SetContent(content);
            _inviteWindow.Closed += (_, _) => Close();
            AddChild(_inviteWindow);
            _inviteInput.FocusInput();
        }

        private void ShowGroups()
        {
            _switching = true;
            _roomWindow?.Hide();
            _groupsWindow ??= CreateWindow(true, out _groupsBack);
            _groupsWindow.Show();
            _switching = false;
            FocusCurrent();
        }

        private void ShowRoom()
        {
            if (!RoomService.CanOpenRoomSettings)
            {
                Close();
                return;
            }
            _switching = true;
            if (_groupsWindow != null)
            {
                _groupsWindow.Hide();
                RemoveChild(_groupsWindow);
                _groupsWindow.QueueFree();
                _groupsWindow = null;
                _groupsBack = null;
            }
            _roomWindow ??= CreateWindow(false, out _roomBack);
            _roomWindow.Show();
            _switching = false;
            FocusCurrent();
        }

        private void BackFromGroups()
        {
            if (_roomWindow != null) ShowRoom();
            else Close();
        }

        private void FocusCurrent()
        {
            if (_inviteWindow is { Visible: true })
            {
                _inviteInput?.FocusInput();
                return;
            }
            var target = _groupsWindow is { Visible: true } ? _groupsBack : _roomBack;
            if (target != null && GodotObject.IsInstanceValid(target) && target.IsVisibleInTree()) target.GrabFocus();
        }

        private void KeepFocusInside(Control control)
        {
            if (!_closing && !_switching && control != null && !IsAncestorOf(control)) FocusCurrent();
        }

        public override void _Input(InputEvent @event)
        {
            if (_closing || !IsVisibleInTree() || @event.IsEcho()) return;
            if (@event is not InputEventKey { Keycode: Key.Escape, Pressed: true } &&
                !@event.IsActionPressed(MegaInput.cancel) && !@event.IsActionPressed(MegaInput.pauseAndBack)) return;
            GetViewport().SetInputAsHandled();
            if (_groupsWindow is { Visible: true }) BackFromGroups();
            else Close();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!_closing && IsVisibleInTree()) GetViewport().SetInputAsHandled();
        }

        public override void _Process(double delta)
        {
            if (_lobby != RoomService.LobbyId || _runInProgress != (RunManager.Instance?.IsInProgress == true) ||
                _roomWindow != null && !RoomService.CanOpenRoomSettings ||
                NModalContainer.Instance?.OpenModal != null ||
                _source != null && (!GodotObject.IsInstanceValid(_source) || !_source.IsVisibleInTree())) Close();
        }

        private void Close()
        {
            if (_closing) return;
            _closing = true;
            Hide();
            _layer.QueueFree();
            var previous = _previousFocus;
            Callable.From(() =>
            {
                if (NModalContainer.Instance?.OpenModal == null && previous != null &&
                    GodotObject.IsInstanceValid(previous) && previous.IsVisibleInTree()) previous.GrabFocus();
            }).CallDeferred();
        }

        public override void _ExitTree()
        {
            GetViewport().GuiFocusChanged -= KeepFocusInside;
            if (ReferenceEquals(_current, this)) _current = null;
        }
    }
}
