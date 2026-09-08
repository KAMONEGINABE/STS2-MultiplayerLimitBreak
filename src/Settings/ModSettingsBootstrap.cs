using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2MultiplayerLimitBreak.Rooms;

namespace STS2MultiplayerLimitBreak.Settings
{
    internal static class ModSettingsBootstrap
    {
        private static readonly Lock InitLock = new();
        private static bool _initialized;

        public static double ExtraPlayerScalingMultiplier => ClampExtraPlayerScalingMultiplier(
            ModData.Settings.ExtraPlayerScalingMultiplier);

        public static void Initialize()
        {
            lock (InitLock)
            {
                if (_initialized) return;

                IModSettingsValueBinding<double> extraPlayerScalingMultiplierBinding = ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, double>(
                        Const.ModId,
                        Const.SettingsKey,
                        _ => RuntimeMultiplayerSettings.ExtraPlayerScalingMultiplier,
                        (_, value) => SetExtraPlayerScalingMultiplier(value)),
                    () => ModSettings.DefaultExtraPlayerScalingMultiplier);

                RitsuLibFramework.RegisterModSettings(Const.ModId, page => page
                    .WithModDisplayName(ModSettingsLocalization.T("mod.displayName", "Multiplayer Limit Break"))
                    .WithTitle(ModSettingsLocalization.T("page.title", "Settings"))
                    .WithDescription(ModSettingsLocalization.T(
                        "page.description",
                        "Raises the multiplayer lobby capacity to 16 players."))
                    .AddSection("room_navigation", section => section
                        .AddButton("match_groups", ModSettingsLocalization.T("rooms.manageGroups", "Manage match groups"),
                            ModSettingsLocalization.T("rooms.open", "Open"), RoomService.OpenMatchGroups))
                    .AddSection("scaling", AddScaling));

                _initialized = true;

                void AddScaling(ModSettingsSectionBuilder section)
                {
                    section.WithReadOnlyOnHostSurfaces(ModSettingsHostSurface.RunPause | ModSettingsHostSurface.CombatPause)
                        .WithEnabledWhen(CanEditSettings)
                        .WithTitle(ModSettingsLocalization.T("section.scaling", "Player Scaling"))
                        .AddSlider("extra_player_scaling_multiplier",
                            ModSettingsLocalization.T("extraPlayerScalingMultiplier.label", "Extra Player Scaling"),
                            extraPlayerScalingMultiplierBinding,
                            ModSettings.MinExtraPlayerScalingMultiplier, ModSettings.MaxExtraPlayerScalingMultiplier,
                            0.05d, value => $"{value:0.00}x",
                            ModSettingsLocalization.T("extraPlayerScalingMultiplier.description",
                                "Values above 4 players scale as 4 plus extra players times this multiplier."));
                }
            }
        }

        private static bool CanEditSettings()
        {
            var session = RoomService.Session ?? RunManager.Instance?.NetService;
            return RunManager.Instance?.IsInProgress != true && session?.Type != NetGameType.Client &&
                   (!RoomService.IsHost || RoomService.CanEditRoom);
        }

        internal static void SetExtraPlayerScalingMultiplier(double value, bool save = false)
        {
            if (!CanEditSettings()) return;
            var previous = ModData.Settings.ExtraPlayerScalingMultiplier;
            ModData.Settings.ExtraPlayerScalingMultiplier = ClampExtraPlayerScalingMultiplier(value);
            if (save)
            {
                try { ModData.Save(); }
                catch { ModData.Settings.ExtraPlayerScalingMultiplier = previous; throw; }
            }
            RuntimeMultiplayerSettings.PublishHostSettings("settings_changed");
        }

        internal static double ClampExtraPlayerScalingMultiplier(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return ModSettings.DefaultExtraPlayerScalingMultiplier;

            return Math.Clamp(
                value,
                ModSettings.MinExtraPlayerScalingMultiplier,
                ModSettings.MaxExtraPlayerScalingMultiplier);
        }
    }
}
