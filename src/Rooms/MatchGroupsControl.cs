using Godot;
using STS2MultiplayerLimitBreak.Settings;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Ui.Toast;

namespace STS2MultiplayerLimitBreak.Rooms
{
    internal sealed partial class MatchGroupsControl : VBoxContainer
    {
        private readonly VBoxContainer _groups = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        private readonly Button _add;
        private readonly Button _save;
        private readonly Label _count;
        private readonly List<MatchGroupSettings> _draft;
        private readonly HashSet<MatchGroupSettings> _expanded = [];
        private readonly List<RoomSecretEdit> _secrets = [];

        public MatchGroupsControl()
        {
            _draft = Clone(RoomService.Groups());
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 10);
            var actions = RoomUi.Row();
            _count = RoomUi.Label("");
            actions.AddChild(_count);
            _add = RoomUi.Button(L("rooms.addGroup", "Add group"), () =>
            {
                if (_draft.Count >= RoomService.MaxGroups) return;
                var group = new MatchGroupSettings();
                _draft.Add(group);
                _expanded.Add(group);
                MarkDirty();
                BuildGroups();
            });
            actions.AddChild(_add);
            _save = RoomUi.Button(L("rooms.saveGroups", "Save"), () =>
            {
                if (!RoomService.SaveGroups(Clone(_draft))) return;
                Conceal();
                _save!.Disabled = true;
                RitsuToastService.ShowInfo(L("rooms.groupsSaved", "Match groups saved."), L("rooms.groupsTitle", "[MLB] Match groups"));
            }, ModSettingsButtonTone.Accent);
            _save.Disabled = true;
            _add.TooltipText = L("rooms.addGroupHint", "Save up to 8 groups, each with its own match code.");
            _save.TooltipText = L("rooms.saveGroupsHint", "Apply changes to group codes and switches.");
            actions.AddChild(_save);
            AddChild(actions);
            _groups.AddThemeConstantOverride("separation", 6);
            AddChild(RoomUi.Scroll(_groups));
            BuildGroups();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationVisibilityChanged && !IsVisibleInTree()) Conceal();
        }

        public override void _ExitTree() => Conceal();

        private void Conceal()
        {
            foreach (var secret in _secrets)
                if (GodotObject.IsInstanceValid(secret)) secret.Conceal();
        }

        private void MarkDirty() => _save.Disabled = false;

        private void BuildGroups()
        {
            Conceal();
            _secrets.Clear();
            RoomUi.Clear(_groups);
            for (var i = 0; i < _draft.Count; i++)
            {
                var group = _draft[i];
                var fallback = string.Format(L("rooms.groupName", "Group {0}"), i + 1);
                var body = new VBoxContainer();
                body.AddThemeConstantOverride("separation", 6);
                var foldout = new RoomFoldout(Title(), body, _expanded.Contains(group));
                foldout.ExpandedChanged += expanded =>
                {
                    if (expanded) _expanded.Add(group);
                    else _expanded.Remove(group);
                };
                AddToggle(L("rooms.search", "Search rooms"), group.Search, value => group.Search = value,
                    L("rooms.searchHint", "Find public rooms using this code in the join list."));
                AddToggle(L("rooms.publish", "Use when hosting"), group.Publish, value => group.Publish = value,
                    L("rooms.publishHint", "Only your public rooms can be found with this code."));
                foldout.Header.AddChild(RoomUi.Button(L("rooms.remove", "Remove"), () =>
                {
                    _draft.Remove(group);
                    _expanded.Remove(group);
                    MarkDirty();
                    BuildGroups();
                }, ModSettingsButtonTone.Danger));
                var name = RoomUi.Input(32, 140);
                name.Text = group.Name;
                name.PlaceholderText = fallback;
                name.TextChanged += value =>
                {
                    group.Name = value;
                    foldout.SetTitle(Title());
                    MarkDirty();
                };
                body.AddChild(RoomUi.Field(L("rooms.groupLabel", "Name"), name,
                    L("rooms.groupLabelHint", "A local label. Your friends can use a different name.")));
                var code = new RoomSecretEdit(group.Code, 128);
                _secrets.Add(code);
                code.ValueChanged += value => { group.Code = value; MarkDirty(); };
                code.AddChild(RoomUi.Button(L("rooms.copy", "Copy"), () =>
                {
                    if (string.IsNullOrWhiteSpace(code.Value)) return;
                    DisplayServer.ClipboardSet(RoomInviteCodec.NormalizeMatchCode(code.Value));
                    RitsuToastService.ShowInfo(L("rooms.matchCopied", "Match code copied."), L("rooms.groupsTitle", "[MLB] Match groups"));
                }, hint: L("rooms.copyMatchHint", "Share this code with friends to use in their match groups.")));
                body.AddChild(RoomUi.Field(L("rooms.matchCode", "Match code"), code,
                    L("rooms.matchCodeHint", "Case-sensitive. Surrounding spaces are ignored.")));
                _groups.AddChild(foldout);

                string Title() => string.IsNullOrWhiteSpace(group.Name) ? fallback : group.Name;

                void AddToggle(string label, bool value, Action<bool> changed, string? hint = null)
                {
                    var toggle = ModSettingsUiControlTheming.CreateCompactSettingsToggleButton(label, value);
                    toggle.CustomMinimumSize = new(RoomUi.ButtonWidth, STS2RitsuLib.Ui.Shell.Theme.RitsuShellTheme.Current.Metric.Entry.ValueMinHeight);
                    toggle.ClipText = true;
                    toggle.TooltipText = hint ?? label;
                    toggle.Toggled += next => { changed(next); MarkDirty(); };
                    foldout.Header.AddChild(toggle);
                }
            }
            _add.Disabled = _draft.Count >= RoomService.MaxGroups;
            _count.Text = $"{_draft.Count} / {RoomService.MaxGroups}";
            if (_draft.Count == 0) _groups.AddChild(RoomUi.Label(L("rooms.emptyGroups", "Add a group to save a match code.")));
        }

        private static List<MatchGroupSettings> Clone(IEnumerable<MatchGroupSettings> groups) => groups.Select(group =>
            new MatchGroupSettings { Name = group.Name ?? "", Code = group.Code ?? "", Search = group.Search, Publish = group.Publish }).ToList();

        private static string L(string key, string fallback) => RoomService.L(key, fallback);
    }
}
