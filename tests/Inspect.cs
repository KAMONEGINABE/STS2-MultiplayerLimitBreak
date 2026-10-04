using System.Reflection;
using System.Runtime.Loader;

var gameDir = @"C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64";
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var path = Path.Combine(gameDir, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};

var sts2 = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(gameDir, "sts2.dll"));
var target = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.IStartRunLobbyListener")!;
foreach (var t in sts2.GetTypes().Where(t => target.IsAssignableFrom(t) && !t.IsInterface))
{
    Console.WriteLine($"Implementer: {t.FullName}");
}
