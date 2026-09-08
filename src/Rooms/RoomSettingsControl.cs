using Godot;
using STS2MultiplayerLimitBreak.Settings;
using STS2RitsuLib.Settings;

namespace STS2MultiplayerLimitBreak.Rooms
{
    internal sealed partial class RoomSettingsControl : VBoxContainer
    {
        private readonly Label _status;
        private readonly Label _invite;
        private readonly Button _reveal;
        private readonly Button _copyCode;
        private readonly Button _copyLink;
        private readonly ModSettingsTextButton _friends;
        private readonly ModSettingsTextButton _public;
        private readonly Label _scaling;
        private readonly Label _scalingSummary;
        private readonly ModSettingsFloatSliderControl _scalingEditor;
        private double _lastMultiplier = double.NaN;
        private bool _revealed;
        private ulong _lastLobby;
        private double _refreshElapsed;

        public RoomSettingsControl()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 10);
            _status = RoomUi.Label("");
            AddChild(_status);
            _friends = RoomUi.Button(L("rooms.friendsOption", "Friends only"), () => RoomService.ChangeVisibility(false));
            _public = RoomUi.Button(L("rooms.publicOption", "Public"), () => RoomService.ChangeVisibility(true));
            _friends.TooltipText = L("rooms.friendsHint", "New rooms start here. Invites do not bypass friend permissions.");
            _public.TooltipText = L("rooms.publicHint", "Non-friends can join by invite or link, or find the room through enabled match groups.");
            var choices = ModSettingsUiControlTheming.CreateSegmentedButtonRow(_friends, _public);
            AddChild(RoomUi.Field(L("rooms.joinMethod", "Who can join"), choices,
                L("rooms.visibilityHint", "New rooms start as friends-only. Match-code discovery requires a public room; match codes are not passwords.")));

            var invitation = RoomUi.Row();
            _invite = RoomUi.Label("••••••••");
            _invite.AutowrapMode = TextServer.AutowrapMode.Off;
            _invite.ClipText = true;
            invitation.AddChild(_invite);
            _reveal = RoomUi.Button(L("rooms.show", "Show"), () => { _revealed = !_revealed; UpdateSession(); });
            _reveal.TooltipText = L("rooms.revealHint", "Toggle visible text. Keep hidden while streaming.");
            invitation.AddChild(_reveal);
            _copyCode = RoomUi.Button(L("rooms.copy", "Copy"), () => RoomService.CopyInvite(false));
            _copyCode.TooltipText = L("rooms.inviteHint", "Share this code for the join screen. It expires when the lobby closes.");
            invitation.AddChild(_copyCode);
            AddChild(RoomUi.Field(L("rooms.inviteCodeLabel", "Invite code"), invitation,
                L("rooms.inviteHint", "Share this code for the join screen. It expires when the lobby closes.")));
            _copyLink = RoomUi.Button(L("rooms.copyLink", "Copy Steam link"), () => RoomService.CopyInvite(true), width: RoomUi.ActionButtonWidth);
            _copyLink.TooltipText = L("rooms.linkHint", "Share a Steam room link. It can also be pasted into Enter invite.");
            var actions = RoomUi.Actions();
            actions.AddChild(_copyLink);
            actions.AddChild(RoomUi.Button(L("rooms.manageGroups", "Manage match groups"), RoomService.OpenMatchGroups,
                width: RoomUi.ActionButtonWidth,
                hint: L("rooms.manageGroupsHint", "Save separate match codes for different groups of friends.")));
            AddChild(actions);

            AddChild(ModSettingsUiFactory.CreateDivider());
            var difficulty = new VBoxContainer();
            difficulty.AddThemeConstantOverride("separation", 8);
            var scalingValue = new VBoxContainer();
            _scalingEditor = new((float)RuntimeMultiplayerSettings.ExtraPlayerScalingMultiplier,
                (float)ModSettings.MinExtraPlayerScalingMultiplier, (float)ModSettings.MaxExtraPlayerScalingMultiplier,
                0.05f, value => $"{value:0.00}×", ChangeScaling)
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            scalingValue.AddChild(_scalingEditor);
            _scaling = RoomUi.Label("");
            _scaling.AutowrapMode = TextServer.AutowrapMode.Off;
            _scaling.HorizontalAlignment = HorizontalAlignment.Right;
            scalingValue.AddChild(_scaling);
            difficulty.AddChild(RoomUi.Field(L("extraPlayerScalingMultiplier.label", "Extra Player Scaling"), scalingValue,
                L("rooms.scalingHint", "Scales players beyond the first four. Default: 1.00×. Host changes sync to everyone in the room.")));
            var foldout = new RoomFoldout(L("section.scaling", "Player scaling"), difficulty, false);
            foldout.SetHint(L("rooms.scalingHint", "Scales players beyond the first four. Default: 1.00×. Host changes sync to everyone in the room."));
            _scalingSummary = RoomUi.Label("");
            _scalingSummary.AutowrapMode = TextServer.AutowrapMode.Off;
            _scalingSummary.CustomMinimumSize = new(72, 0);
            _scalingSummary.HorizontalAlignment = HorizontalAlignment.Right;
            _scalingSummary.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            _scalingSummary.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            RoomUi.Hint(_scalingSummary, L("rooms.scalingHint", "Scales players beyond the first four. Default: 1.00×. Host changes sync to everyone in the room."));
            foldout.Header.AddChild(_scalingSummary);
            foldout.ExpandedChanged += expanded => _scalingSummary.Visible = !expanded;
            AddChild(foldout);
        }

        public override void _Ready()
        {
            RoomService.Changed += UpdateSession;
            UpdateSession();
        }

        public override void _ExitTree()
        {
            RoomService.Changed -= UpdateSession;
            Conceal();
        }

        public override void _Process(double delta)
        {
            _refreshElapsed += delta;
            if (_refreshElapsed < 0.5 || !IsVisibleInTree()) return;
            _refreshElapsed = 0;
            UpdateSession();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationVisibilityChanged && !IsVisibleInTree() && _invite != null) Conceal();
        }

        private void Conceal()
        {
            _revealed = false;
            _invite.Text = "••••••••";
            _reveal.Text = L("rooms.show", "Show");
        }

        private void UpdateSession()
        {
            if (!IsInsideTree()) return;
            var lobby = RoomService.LobbyId;
            if (lobby != _lastLobby) { _lastLobby = lobby; Conceal(); }
            var active = RoomService.Available && lobby != 0;
            _status.Text = !RoomService.Available ? L("rooms.steamUnavailable", "Steam room sharing is unavailable.")
                : !active ? L("rooms.noRoom", "No active Steam room.")
                : !RoomService.IsHost ? L("rooms.clientRoom", "Room settings are controlled by the host.")
                : !RoomService.CanEditRoom ? L("rooms.closedRoom", "The run has started. Room settings are locked.") : "";
            _status.Visible = _status.Text.Length > 0;
            _friends.Disabled = _public.Disabled = !RoomService.CanEditRoom;
            _friends.SetSelected(!RoomService.IsPublic);
            _public.SetSelected(RoomService.IsPublic);
            var multiplier = RuntimeMultiplayerSettings.ExtraPlayerScalingMultiplier;
            _scaling.Text = _scalingSummary.Text = $"{multiplier:0.00}×";
            _scalingEditor.Visible = RoomService.CanEditRoom;
            _scaling.Visible = !RoomService.CanEditRoom;
            if (multiplier != _lastMultiplier)
            {
                _lastMultiplier = multiplier;
                _scalingEditor.SetValue((float)multiplier);
            }
            _copyCode.Disabled = _copyLink.Disabled = _reveal.Disabled = !active;
            _invite.Text = active && _revealed ? RoomInviteCodec.Encode(lobby) : "••••••••";
            _reveal.Text = L(_revealed ? "rooms.hide" : "rooms.show", _revealed ? "Hide" : "Show");
        }

        private void ChangeScaling(float value)
        {
            if (!RoomService.CanEditRoom) return;
            try
            {
                ModSettingsBootstrap.SetExtraPlayerScalingMultiplier(
                    Math.Round(value, 2), true);
            }
            catch (Exception ex) when (RoomService.Recoverable(ex)) { RoomService.Report(ex); }
            _lastMultiplier = double.NaN;
            UpdateSession();
        }

        private static string L(string key, string fallback) => RoomService.L(key, fallback);
    }
}
