using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Runs;
using STS2MultiplayerLimitBreak.Settings;
using STS2RitsuLib.Ui.Toast;

namespace STS2MultiplayerLimitBreak.Rooms
{
    internal static class RoomService
    {
        public const int MaxGroups = 8;
        private static INetGameService? _session;
        private static ulong _hostLobby;
        private static bool _public;
        private static bool _closed;
        private static readonly HashSet<string> PublishedTags = [];

        public static SteamRoomBridge? Bridge { get; private set; }
        public static bool Available => Bridge != null && SteamInitializer.Initialized;
        public static int SettingsRevision { get; private set; }
        public static event Action? Changed;
        public static INetGameService? Session => _session is { IsConnected: true, Platform: PlatformType.Steam } ? _session : null;
        public static bool IsHost => Session?.Type == NetGameType.Host;
        public static bool CanOpenRoomSettings => Available && LobbyId != 0 &&
                                                  RunManager.Instance?.IsInProgress != true && (!IsHost || !_closed);
        public static bool CanEditRoom => CanOpenRoomSettings && IsHost && LobbyId == _hostLobby;
        public static bool IsPublic => CanEditRoom && _public;
        public static ulong LobbyId => ulong.TryParse(Session?.GetRawLobbyIdentifier(), out var id) && RoomInviteCodec.IsLobbyId(id) ? id : 0;

        public static bool Initialize()
        {
            try
            {
                Bridge = SteamRoomBridge.TryCreate();
                return Bridge != null;
            }
            catch (Exception ex) when (Recoverable(ex))
            {
                Report(ex, false);
                return false;
            }
        }

        public static void Disable()
        {
            Bridge = null;
        }

        public static bool Recoverable(Exception ex)
        {
            if (ex is System.Reflection.TargetInvocationException { InnerException: { } inner })
                return Recoverable(inner);
            return ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException);
        }

        public static void Bind(INetGameService session)
        {
            if (session.Platform != PlatformType.Steam) return;
            _session = session;
            Changed?.Invoke();
        }

        public static void HostCreated(INetGameService session)
        {
            if (!Available || !session.IsConnected || session.Platform != PlatformType.Steam) return;
            _session = session;
            _hostLobby = LobbyId;
            _closed = false;
            _public = false;
            PublishedTags.Clear();
            Bridge!.SetPublic(_hostLobby, false);
            Changed?.Invoke();
            if (ModData.Settings.RoomSettingsIntroductionShown) return;
            var lobby = _hostLobby;
            RitsuToastService.ShowInfo(
                L("rooms.introduction", "Rooms now start as friends-only. Click here or the lobby gear to share invites, make your room public, and manage match groups."),
                L("rooms.introductionTitle", "[MLB] New room settings"),
                () =>
                {
                    if (LobbyId == lobby && CanEditRoom) OpenSettings();
                });
            ModData.Settings.RoomSettingsIntroductionShown = true;
            ModData.Save();
        }

        public static void HostClosed(bool closed)
        {
            if (!IsHost) return;
            _closed = closed;
            if (closed)
            {
                _public = false;
                ClearPublished();
            }
            else if (_public)
            {
                Bridge!.SetPublic(_hostLobby, true);
            }
            Changed?.Invoke();
        }

        public static void OpenSettings()
        {
            OpenPopup(false);
        }

        public static void OpenMatchGroups()
        {
            if (!Available) return;
            OpenPopup(true);
        }

        private static void OpenPopup(bool groups)
        {
            try
            {
                if (!RoomSettingsPopup.Open(groups))
                    RitsuToastService.ShowWarning(L("rooms.openFailed", "Room settings are unavailable on this screen."));
            }
            catch (Exception ex) when (Recoverable(ex)) { Report(ex); }
        }

        public static void ChangeVisibility(bool makePublic)
        {
            if (!CanEditRoom) return;
            try
            {
                if (makePublic)
                {
                    PublishGroups();
                    Bridge!.SetPublic(_hostLobby, true);
                    _public = true;
                }
                else
                {
                    Bridge!.SetPublic(_hostLobby, false);
                    _public = false;
                    ClearPublished();
                }
            }
            catch (Exception ex) when (Recoverable(ex)) { Report(ex); }
            Changed?.Invoke();
        }

        private static void PublishGroups()
        {
            if (Bridge == null || !CanEditRoom) return;
            var tags = Groups().Where(group => group.Publish && !string.IsNullOrWhiteSpace(group.Code))
                .Select(group => RoomInviteCodec.MatchTag(group.Code)).ToHashSet(StringComparer.Ordinal);
            if (Bridge.Data(_hostLobby, "mlb_discovery").Length > 0) Bridge.DeleteData(_hostLobby, "mlb_discovery");
            foreach (var tag in PublishedTags.Except(tags).ToArray())
            {
                Bridge.DeleteData(_hostLobby, tag);
                PublishedTags.Remove(tag);
            }
            foreach (var tag in tags)
            {
                Bridge.SetData(_hostLobby, tag, "1");
                PublishedTags.Add(tag);
            }
            if (tags.Count > 0) Bridge.SetData(_hostLobby, "mlb_discovery", "1");
            else if (Bridge.Data(_hostLobby, "mlb_discovery").Length > 0) Bridge.DeleteData(_hostLobby, "mlb_discovery");
        }

        private static void ClearPublished()
        {
            if (Bridge == null || _hostLobby == 0) return;
            if (Bridge.Data(_hostLobby, "mlb_discovery").Length > 0) Bridge.DeleteData(_hostLobby, "mlb_discovery");
            foreach (var tag in PublishedTags.ToArray())
            {
                Bridge.DeleteData(_hostLobby, tag);
                PublishedTags.Remove(tag);
            }
        }

        public static IReadOnlyList<MatchGroupSettings> Groups()
        {
            return (ModData.Settings.MatchGroups ?? []).Where(group => group != null).Take(MaxGroups)
                .Select(group => new MatchGroupSettings
                {
                    Name = new string((group.Name ?? "").Where(c => !char.IsControl(c)).Take(32).ToArray()),
                    Code = group.Code is { Length: <= 128 } code && !code.Any(char.IsControl) && Encoding.UTF8.GetByteCount(code) <= 256 ? code : "",
                    Search = group.Search,
                    Publish = group.Publish,
                }).ToArray();
        }

        public static bool SaveGroups(List<MatchGroupSettings> groups)
        {
            try
            {
                if (groups.Count > MaxGroups || groups.Any(group => group.Name.Length > 32 ||
                        group.Name.Any(char.IsControl) || group.Code.Length > 128 || group.Code.Any(char.IsControl) ||
                        Encoding.UTF8.GetByteCount(group.Code) > 256))
                {
                    RitsuToastService.ShowWarning(L("rooms.invalidGroup", "Use names up to 32 characters and codes up to 128 characters / 256 UTF-8 bytes, without control characters."));
                    return false;
                }
                foreach (var group in groups) group.Code = RoomInviteCodec.NormalizeMatchCode(group.Code);
                var codes = groups.Where(group => group.Code.Length > 0).Select(group => group.Code).ToArray();
                if (codes.Distinct(StringComparer.Ordinal).Count() != codes.Length)
                {
                    RitsuToastService.ShowWarning(L("rooms.duplicateCode", "This match code is already saved in another group."));
                    return false;
                }
                var previous = ModData.Settings.MatchGroups;
                ModData.Settings.MatchGroups = groups;
                try { ModData.Save(); }
                catch { ModData.Settings.MatchGroups = previous; throw; }
                SettingsRevision++;
                if (IsPublic)
                {
                    try { PublishGroups(); }
                    catch (Exception ex) when (Recoverable(ex))
                    {
                        Report(ex, false);
                        RitsuToastService.ShowWarning(L("rooms.publishFailed", "Match groups were saved, but Steam publication failed. Retry making the room public."));
                    }
                }
                Changed?.Invoke();
                return true;
            }
            catch (Exception ex) when (Recoverable(ex)) { Report(ex); return false; }
        }

        public static void CopyInvite(bool link)
        {
            if (!CanOpenRoomSettings) return;
            try
            {
                var value = link ? RoomInviteCodec.Link(LobbyId, Bridge!.Owner(LobbyId)) : RoomInviteCodec.Encode(LobbyId);
                DisplayServer.ClipboardSet(value);
                RitsuToastService.ShowInfo(L(link ? "rooms.linkCopied" : "rooms.inviteCopied", link ? "Steam link copied." : "Invitation code copied."));
            }
            catch (Exception ex) when (Recoverable(ex)) { Report(ex); }
        }

        public static void Report(Exception ex, bool notify = true)
        {
            Log.Warn($"Room sharing operation failed ({ex.GetType().Name}).");
            if (notify) RitsuToastService.ShowWarning(L("rooms.operationFailed", "The operation could not be completed. Check Steam connectivity and try again."));
        }

        public static string L(string key, string fallback) => ModSettingsLocalization.Get(key, fallback);
    }
}
