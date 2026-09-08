using System.Reflection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Platform.Steam;

namespace STS2MultiplayerLimitBreak.Rooms
{
    internal sealed class SteamRoomBridge
    {
        private readonly Type _matchmaking;
        private readonly Type _steamId;
        private readonly Type _friends;
        private readonly Type _lobbyType;
        private readonly Type _comparison;
        private readonly Type _distance;
        private readonly Type _callResult;
        private readonly Type _matchList;
        private readonly Type _friendInfo;
        private readonly Dictionary<(Type, string, int), MethodInfo> _methods = [];
        private static readonly SemaphoreSlim SearchGate = new(1, 1);

        private SteamRoomBridge(Assembly steam)
        {
            Type Require(string name) => steam.GetType("Steamworks." + name, true)!;
            _matchmaking = Require("SteamMatchmaking");
            _steamId = Require("CSteamID");
            _friends = Require("SteamFriends");
            _lobbyType = Require("ELobbyType");
            _comparison = Require("ELobbyComparison");
            _distance = Require("ELobbyDistanceFilter");
            _matchList = Require("LobbyMatchList_t");
            _friendInfo = Require("FriendGameInfo_t");
            _callResult = typeof(INetGameService).Assembly.GetType(
                "MegaCrit.Sts2.Core.Multiplayer.Transport.Steam.SteamCallResult`1", true)!.MakeGenericType(_matchList);
            foreach (var (name, count) in new[]
                     {
                         ("SetLobbyData", 3), ("DeleteLobbyData", 2), ("GetLobbyData", 2), ("SetLobbyType", 2),
                         ("GetLobbyOwner", 1), ("GetNumLobbyMembers", 1), ("GetLobbyMemberLimit", 1),
                         ("RequestLobbyData", 1),
                         ("AddRequestLobbyListStringFilter", 3), ("AddRequestLobbyListDistanceFilter", 1),
                         ("AddRequestLobbyListResultCountFilter", 1), ("RequestLobbyList", 0), ("GetLobbyByIndex", 1),
                     })
                Method(_matchmaking, name, count);
            Method(_friends, "GetFriendGamePlayed", 2);
            Method(_friends, "GetFriendPersonaName", 1);
            Method(_friends, "RequestUserInformation", 2);
        }

        public static SteamRoomBridge? TryCreate()
        {
            if (!SteamInitializer.Initialized) return null;
            var steam = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(
                assembly => assembly.GetType("Steamworks.SteamMatchmaking") != null);
            return steam == null ? null : new(steam);
        }

        private MethodInfo Method(Type type, string name, int count)
        {
            var key = (type, name, count);
            if (_methods.TryGetValue(key, out var method)) return method;
            method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(info => info.Name == name && info.GetParameters().Length == count);
            _methods.Add(key, method);
            return method;
        }

        private object? Call(Type type, string name, params object?[] args)
        {
            return Method(type, name, args.Length).Invoke(null, args);
        }

        private object Id(ulong value) => Activator.CreateInstance(_steamId, value)!;
        private static ulong IdValue(object id) => (ulong)id.GetType().GetField("m_SteamID")!.GetValue(id)!;

        public ulong Owner(ulong lobby) => IdValue(Call(_matchmaking, "GetLobbyOwner", Id(lobby))!);
        public int Members(ulong lobby) => (int)Call(_matchmaking, "GetNumLobbyMembers", Id(lobby))!;
        public int Limit(ulong lobby) => (int)Call(_matchmaking, "GetLobbyMemberLimit", Id(lobby))!;
        public string Data(ulong lobby, string key) => (string)Call(_matchmaking, "GetLobbyData", Id(lobby), key)!;
        public void RequestData(ulong lobby) => Call(_matchmaking, "RequestLobbyData", Id(lobby));

        public void SetData(ulong lobby, string key, string value)
        {
            if (!(bool)Call(_matchmaking, "SetLobbyData", Id(lobby), key, value)!)
                throw new InvalidOperationException("Steam rejected lobby metadata.");
        }

        public void DeleteData(ulong lobby, string key)
        {
            if (!(bool)Call(_matchmaking, "DeleteLobbyData", Id(lobby), key)!)
                throw new InvalidOperationException("Steam rejected lobby metadata removal.");
        }

        public void SetPublic(ulong lobby, bool isPublic)
        {
            if (!(bool)Call(_matchmaking, "SetLobbyType", Id(lobby), Enum.ToObject(_lobbyType, isPublic ? 2 : 1))!)
                throw new InvalidOperationException("Steam rejected lobby visibility.");
        }

        public ulong FriendLobby(ulong friend)
        {
            object?[] args = [Id(friend), Activator.CreateInstance(_friendInfo)];
            if (!(bool)Call(_friends, "GetFriendGamePlayed", args)!) return 0;
            return IdValue(_friendInfo.GetField("m_steamIDLobby")!.GetValue(args[1])!);
        }

        public string PlayerName(ulong player)
        {
            if (player == 0) return "";
            Call(_friends, "RequestUserInformation", Id(player), true);
            var name = (string)Call(_friends, "GetFriendPersonaName", Id(player))!;
            return new string(name.Where(c => !char.IsControl(c)).Take(48).ToArray());
        }

        public async Task SearchGroups(IReadOnlyList<(string Tag, string Name)> groups,
            Action<ulong, string> found, CancellationToken cancellationToken)
        {
            await SearchGate.WaitAsync(cancellationToken);
            try
            {
                foreach (var (tag, name) in groups)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Call(_matchmaking, "AddRequestLobbyListStringFilter", "mlb_discovery", "1", Enum.ToObject(_comparison, 0));
                    Call(_matchmaking, "AddRequestLobbyListStringFilter", tag, "1", Enum.ToObject(_comparison, 0));
                    Call(_matchmaking, "AddRequestLobbyListDistanceFilter", Enum.ToObject(_distance, 3));
                    Call(_matchmaking, "AddRequestLobbyListResultCountFilter", 50);
                    var call = Call(_matchmaking, "RequestLobbyList");
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, SteamInitializer.DisconnectToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(22));
                    using var result = (IDisposable)Activator.CreateInstance(_callResult, call, timeout.Token)!;
                    var task = (Task)_callResult.GetProperty("Task")!.GetValue(result)!;
                    await task;
                    cancellationToken.ThrowIfCancellationRequested();
                    var response = task.GetType().GetProperty("Result")!.GetValue(task)!;
                    var count = Math.Min(50, (uint)_matchList.GetField("m_nLobbiesMatching")!.GetValue(response)!);
                    for (var i = 0; i < count; i++)
                    {
                        var lobby = IdValue(Call(_matchmaking, "GetLobbyByIndex", i)!);
                        if (RoomInviteCodec.IsLobbyId(lobby) && Data(lobby, "mlb_discovery") == "1" && Data(lobby, tag) == "1")
                            found(lobby, name);
                    }
                }
            }
            finally
            {
                SearchGate.Release();
            }
        }
    }
}
