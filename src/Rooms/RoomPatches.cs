using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2RitsuLib;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace STS2MultiplayerLimitBreak.Rooms
{
    internal static class RoomPatches
    {
        public static void Initialize()
        {
            if (!RoomService.Initialize()) return;
            var patcher = RitsuLibFramework.CreatePatcher(Const.ModId, "room_sharing", "room sharing and discovery");
            patcher.RegisterPatch<HostStartedPatch>();
            patcher.RegisterPatch<HostClosedPatch>();
            patcher.RegisterPatch<ShowFriendsPatch>();
            patcher.RegisterPatch<JoinScreenClosedPatch>();
            patcher.RegisterPatch<LoadLobbySettingsPatch>();
            if (!patcher.PatchAll()) RoomService.Disable();
        }

        private sealed class HostStartedPatch : IPatchMethod
        {
            public static string PatchId => "mlb_room_host_created";
            public static string Description => "Initialize room sharing after successful Steam hosting";
            public static ModPatchTarget[] GetTargets() => [new(typeof(NetHostGameService), nameof(NetHostGameService.StartSteamHost))];
            private static void Postfix(NetHostGameService __instance, Task __result)
            {
                _ = Complete(__instance, __result);
            }
            private static async Task Complete(NetHostGameService host, Task pending)
            {
                try
                {
                    await pending;
                    if (host.IsConnected) RoomService.HostCreated(host);
                }
                catch (Exception ex) when (RoomService.Recoverable(ex)) { RoomService.Report(ex); }
            }
        }

        private sealed class HostClosedPatch : IPatchMethod
        {
            public static string PatchId => "mlb_room_host_closed";
            public static string Description => "Remove discovery metadata when the game closes its lobby";
            public static ModPatchTarget[] GetTargets() =>
            [new(typeof(INetGameService).Assembly.GetType("MegaCrit.Sts2.Core.Multiplayer.Transport.Steam.SteamHost", true)!, "SetHostIsClosed")];
            private static void Postfix(bool isClosed)
            {
                if (!RoomService.Available) return;
                try { RoomService.HostClosed(isClosed); }
                catch (Exception ex) when (RoomService.Recoverable(ex)) { RoomService.Report(ex); }
            }
        }

        private sealed class ShowFriendsPatch : IPatchMethod
        {
            public static string PatchId => "mlb_room_join_browser";
            public static string Description => "Include match-group rooms and invitation input in the join screen";
            public static ModPatchTarget[] GetTargets() => [new(typeof(NJoinFriendScreen), "ShowFriends")];
            private static void Postfix(NJoinFriendScreen __instance, ref Task __result)
            {
                if (!RoomService.Available) return;
                try
                {
                    __result = RoomBrowser.Attach(__instance).AfterFriendsAsync(__result);
                }
                catch (Exception ex) when (RoomService.Recoverable(ex))
                {
                    RoomService.Report(ex);
                }
            }
        }

        private sealed class JoinScreenClosedPatch : IPatchMethod
        {
            public static string PatchId => "mlb_room_browser_closed";
            public static string Description => "Cancel discovery and conceal invitation input when leaving the join screen";
            public static ModPatchTarget[] GetTargets() => [new(typeof(NJoinFriendScreen), nameof(NJoinFriendScreen.OnSubmenuClosed))];
            private static void Postfix(NJoinFriendScreen __instance) => RoomBrowser.Find(__instance)?.Deactivate();
        }

        private sealed class LoadLobbySettingsPatch : IPatchMethod
        {
            public static string PatchId => "mlb_load_room_settings";
            public static string Description => "Make room sharing settings accessible in loaded-run lobbies";
            public static ModPatchTarget[] GetTargets() => [new(typeof(NRemoteLoadLobbyPlayerContainer), nameof(NRemoteLoadLobbyPlayerContainer.Initialize))];
            private static void Postfix(NRemoteLoadLobbyPlayerContainer __instance, LoadRunLobby runLobby)
            {
                if (!RoomService.Available) return;
                RoomService.Bind(runLobby.NetService);
                RoomUi.AttachSettingsButton(__instance, runLobby.NetService);
            }
        }
    }
}
