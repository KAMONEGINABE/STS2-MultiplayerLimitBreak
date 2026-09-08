using System.Reflection;
using System.Runtime.Loader;
using STS2MultiplayerLimitBreak.Rooms;

var checks = 0;
void Check(bool condition, string description)
{
    checks++;
    if (!condition) throw new InvalidOperationException(description);
}

const ulong lobbyBase = (1UL << 56) | (8UL << 52) | (0x40000UL << 32);
const ulong owner = 76561197960265729;
var random = new Random(7491);
for (var i = 0; i < 2000; i++)
{
    var lobby = lobbyBase | (uint)random.NextInt64(1, (long)uint.MaxValue + 1);
    var code = RoomInviteCodec.Encode(lobby);
    Check(RoomInviteCodec.TryDecode(code, out var decoded) && decoded == lobby, "Invitation round trip");
    Check(RoomInviteCodec.TryDecode(" \t" + code.ToLowerInvariant() + " \n", out decoded) && decoded == lobby, "Case and surrounding whitespace");
    Check(RoomInviteCodec.TryDecode(RoomInviteCodec.Link(lobby, owner), out decoded) && decoded == lobby, "Steam URL round trip");
    var corrupted = code[..^1] + (code[^1] == '0' ? '1' : '0');
    Check(!RoomInviteCodec.TryDecode(corrupted, out _), "Checksum rejects changed invitation");
}

var id = lobbyBase | 4567;
foreach (var invalid in new string?[]
         {
             null, "", " ", "MLB1-00000-00000-00000", "MLB2-00000-00000-00000", "MLB1-ZZZZZ-ZZZZZ-ZZZZZ",
             "https://example.org/", $"steam://connect/{id}", $"steam://joinlobby/480/{id}/{owner}",
             $"steam://user@joinlobby/2868840/{id}/{owner}", $"steam://joinlobby:123/2868840/{id}/{owner}",
             $"steam://joinlobby/2868840/{owner}/{owner}", $"steam://joinlobby/2868840/{id}/{owner}?x=1",
             $"steam://joinlobby/2868840/{id}/{owner}#fragment", $"steam://joinlobby/2868840/{id}/0",
             $"steam://joinlobby/2868840/{id}/bad", $"steam://joinlobby/2868840/{id}/{owner}/extra",
             $"steam://joinlobby/2868840/-{id}", $"steam://joinlobby/2868840/%31{id}", new string('A', 257),
         })
    Check(!RoomInviteCodec.TryDecode(invalid, out _), "Reject malformed or foreign invitation");
Check(RoomInviteCodec.TryDecode($"steam://joinlobby/2868840/{id}", out var withoutOwner) && withoutOwner == id, "Lobby URL without friend ID");
Check(RoomInviteCodec.MatchTag("  café ") == RoomInviteCodec.MatchTag("cafe\u0301"), "Match code Unicode normalization");
Check(RoomInviteCodec.MatchTag("Group") != RoomInviteCodec.MatchTag("group"), "Match codes remain case-sensitive");
Check(RoomInviteCodec.MatchTag("一组朋友").Length < 255, "Metadata key stays within Steam bounds");
Check(!RoomInviteCodec.MatchTag("private-code").Contains("private-code", StringComparison.Ordinal), "Metadata does not contain the original code");

if (args.Length == 2)
{
    var gamePath = Path.GetFullPath(args[0]);
    var modPath = Path.GetFullPath(args[1]);
    AssemblyLoadContext.Default.Resolving += (_, name) =>
    {
        foreach (var directory in new[] { gamePath, Path.GetDirectoryName(modPath)!,
                     Path.GetFullPath(Path.Combine(gamePath, "..", "mods", "STS2-RitsuLib")) })
        {
            var candidate = Path.Combine(directory, name.Name + ".dll");
            if (File.Exists(candidate)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
        }
        return null;
    };
    var game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(gamePath, "sts2.dll"));
    var steam = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(gamePath, "Steamworks.NET.dll"));
    var mod = AssemblyLoadContext.Default.LoadFromAssemblyPath(modPath);
    Check(mod.GetReferencedAssemblies().All(assembly => assembly.Name != "Steamworks.NET"), "No direct Steamworks assembly dependency");
    var bridgeType = mod.GetType("STS2MultiplayerLimitBreak.Rooms.SteamRoomBridge", true)!;
    var bridge = Activator.CreateInstance(bridgeType, BindingFlags.Instance | BindingFlags.NonPublic, null, [steam], null);
    Check(bridge != null, "All reflected Steam methods resolve against the installed SDK");
    var callResult = game.GetType("MegaCrit.Sts2.Core.Multiplayer.Transport.Steam.SteamCallResult`1", true)!
        .MakeGenericType(steam.GetType("Steamworks.LobbyMatchList_t", true)!);
    Check(callResult.GetConstructor([steam.GetType("Steamworks.SteamAPICall_t", true)!, typeof(CancellationToken)]) != null,
        "Game asynchronous Steam result adapter is compatible");
    foreach (var (name, method) in new[]
             {
                 ("MegaCrit.Sts2.Core.Multiplayer.NetHostGameService", "StartSteamHost"),
                 ("MegaCrit.Sts2.Core.Multiplayer.Transport.Steam.SteamHost", "SetHostIsClosed"),
                 ("MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NJoinFriendScreen", "ShowFriends"),
                 ("MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NJoinFriendScreen", "OnSubmenuClosed"),
                 ("MegaCrit.Sts2.Core.Nodes.Multiplayer.NRemoteLoadLobbyPlayerContainer", "Initialize"),
             })
        Check(game.GetType(name, true)!.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null,
            "Game patch target exists: " + method);

    var harmonyAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(gamePath, "0Harmony.dll"));
    var harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", true)!;
    var harmonyMethodType = harmonyAssembly.GetType("HarmonyLib.HarmonyMethod", true)!;
    var harmony = Activator.CreateInstance(harmonyType, "multiplayer-limit-break.room-sharing-checks")!;
    var patchMethod = harmonyType.GetMethods().Single(method => method.Name == "Patch" && method.GetParameters().Length == 5);
    foreach (var patch in mod.GetType("STS2MultiplayerLimitBreak.Rooms.RoomPatches", true)!.GetNestedTypes(BindingFlags.NonPublic))
    {
        var getTargets = patch.GetMethod("GetTargets", BindingFlags.Public | BindingFlags.Static);
        if (getTargets == null) continue;
        var targets = (Array)getTargets.Invoke(null, null)!;
        foreach (var target in targets)
        {
            var targetType = target!.GetType();
            var ownerType = (Type)targetType.GetProperty("TargetType")!.GetValue(target)!;
            var methodName = (string)targetType.GetProperty("MethodName")!.GetValue(target)!;
            var original = ownerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
            object? Wrap(string name)
            {
                var method = patch.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
                return method == null ? null : Activator.CreateInstance(harmonyMethodType, method);
            }
            patchMethod.Invoke(harmony, [original, Wrap("Prefix"), Wrap("Postfix"), null, null]);
            Check(true, "Harmony can generate wrapper for " + patch.Name);
        }
    }
}

Console.WriteLine($"Passed {checks} room-sharing checks.");
