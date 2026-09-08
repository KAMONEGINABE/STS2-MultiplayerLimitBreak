using Godot;
using MegaCrit.Sts2.Core.Multiplayer.Connection;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace STS2MultiplayerLimitBreak.Rooms
{
    internal sealed partial class RoomBrowser : Node
    {
        private const string NodeName = "MlbRoomBrowser";
        private NJoinFriendScreen _screen = null!;
        private readonly Dictionary<ulong, NJoinFriendButton> _matches = [];
        private CancellationTokenSource? _search;
        private bool _active;
        private bool _joining;
        private bool _refreshingFriends;
        private int _generation;
        private int _revision;
        private double _elapsed;

        public static RoomBrowser? Find(NJoinFriendScreen screen) => screen.GetNodeOrNull<RoomBrowser>(NodeName);

        public static RoomBrowser Attach(NJoinFriendScreen screen)
        {
            var browser = Find(screen);
            if (browser == null)
            {
                browser = new() { Name = NodeName, _screen = screen };
                screen.AddChild(browser);
                var refresh = screen.GetNode<NJoinFriendRefreshButton>("%RefreshButton");
                var invite = RoomJoinActionButton.Create(refresh, "MlbJoinInvite", -1,
                    RoomService.L("rooms.joinInvite", "Enter invite"),
                    () => RoomSettingsPopup.OpenInvite(browser._screen, lobby => _ = browser.JoinAsync(lobby)));
                var groups = RoomJoinActionButton.Create(refresh, "MlbMatchGroups", 1,
                    RoomService.L("rooms.matchGroupsLabel", "Match groups"), RoomService.OpenMatchGroups);
                screen.AddChild(invite);
                screen.AddChild(groups);
                var overlayIndex = screen.GetNode<Control>("%LoadingOverlay").GetIndex();
                screen.MoveChild(invite, overlayIndex);
                screen.MoveChild(groups, overlayIndex + 1);
            }
            browser._active = true;
            return browser;
        }

        public override void _ExitTree() => Deactivate();

        public void Deactivate()
        {
            _active = false;
            _generation++;
            _search?.Cancel();
        }

        public override void _Process(double delta)
        {
            if (!_active || !_screen.IsVisibleInTree() || !RoomService.Available || _joining || _refreshingFriends ||
                _screen.GetNode<Control>("%LoadingOverlay").Visible) return;
            _elapsed += delta;
            if (_search == null && (_elapsed >= 15 || _revision != RoomService.SettingsRevision))
                _ = RefreshMatchesAsync();
        }

        public async Task AfterFriendsAsync(Task original)
        {
            var generation = ++_generation;
            _search?.Cancel();
            _refreshingFriends = true;
            try
            {
                await original;
                if (_active && generation == _generation && IsInsideTree()) await RefreshMatchesAsync();
            }
            finally
            {
                _refreshingFriends = false;
            }
        }

        private async Task RefreshMatchesAsync()
        {
            if (!_active || _joining || !RoomService.Available) return;
            _search?.Cancel();
            using var cancellation = new CancellationTokenSource();
            _search = cancellation;
            var generation = ++_generation;
            _revision = RoomService.SettingsRevision;
            _elapsed = 0;
            try
            {
                var groups = RoomService.Groups().Where(group => group.Search && !string.IsNullOrWhiteSpace(group.Code))
                    .Select((group, index) => (Tag: RoomInviteCodec.MatchTag(group.Code),
                        Name: string.IsNullOrWhiteSpace(group.Name) ? string.Format(RoomService.L("rooms.groupName", "Group {0}"), index + 1) : group.Name))
                    .DistinctBy(group => group.Tag).ToArray();
                var results = new Dictionary<ulong, string>();
                await RoomService.Bridge!.SearchGroups(groups, (lobby, group) => results.TryAdd(lobby, group), cancellation.Token);
                if (!Current()) return;

                var container = _screen.GetNode<Control>("%ButtonContainer");
                foreach (var stale in _matches.Where(pair => !GodotObject.IsInstanceValid(pair.Value) || pair.Value.IsQueuedForDeletion()).Select(pair => pair.Key).ToArray())
                    _matches.Remove(stale);
                var friendLobbies = container.GetChildren().OfType<NJoinFriendButton>()
                    .Where(button => !button.IsQueuedForDeletion() && !_matches.ContainsValue(button))
                    .Select(button => RoomService.Bridge.FriendLobby(button.PlayerId)).ToHashSet();
                results.Remove(RoomService.LobbyId);
                foreach (var lobby in friendLobbies) results.Remove(lobby);
                foreach (var stale in _matches.Keys.Except(results.Keys).ToArray())
                {
                    var button = _matches[stale];
                    container.RemoveChild(button);
                    button.QueueFree();
                    _matches.Remove(stale);
                }
                foreach (var (lobby, group) in results)
                {
                    if (_matches.ContainsKey(lobby)) continue;
                    var owner = RoomService.Bridge.Owner(lobby);
                    if (owner == 0) continue;
                    var button = NJoinFriendButton.Create(owner);
                    button.TooltipText = group;
                    button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(button => _ = JoinAsync(lobby)));
                    container.AddChild(button);
                    _matches.Add(lobby, button);
                }
                _screen.GetNode<Control>("%NoFriendsText").Visible = !container.GetChildren().Any(child => !child.IsQueuedForDeletion());
                ActiveScreenContext.Instance.Update();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (RoomService.Recoverable(ex))
            {
                RoomService.Report(ex, false);
            }
            finally
            {
                if (ReferenceEquals(_search, cancellation))
                {
                    _search = null;
                    _elapsed = 0;
                }
            }

            bool Current() => _active && generation == _generation && IsInsideTree() && !cancellation.IsCancellationRequested;
        }

        private async Task JoinAsync(ulong lobby)
        {
            if (_joining || !_active || !RoomService.Available) return;
            _joining = true;
            _search?.Cancel();
            try
            {
                await _screen.JoinGameAsync(SteamClientConnectionInitializer.FromLobby(lobby));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (RoomService.Recoverable(ex)) { RoomService.Report(ex, false); }
            finally { _joining = false; }
        }
    }
}
