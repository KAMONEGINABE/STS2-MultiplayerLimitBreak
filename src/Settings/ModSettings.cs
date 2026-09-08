namespace STS2MultiplayerLimitBreak.Settings
{
    public sealed class ModSettings
    {
        public const double MinExtraPlayerScalingMultiplier = 0.0d;

        public const double MaxExtraPlayerScalingMultiplier = 2.0d;

        public const double DefaultExtraPlayerScalingMultiplier = 1.0d;

        public int DataVersion { get; set; } = 2;

        public double ExtraPlayerScalingMultiplier { get; set; } = DefaultExtraPlayerScalingMultiplier;

        // Persist across restarts and version upgrades; this introduction is shown only once.
        public bool RoomSettingsIntroductionShown { get; set; }

        public List<MatchGroupSettings> MatchGroups { get; set; } = [];
    }

    public sealed class MatchGroupSettings
    {
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public bool Search { get; set; } = true;
        public bool Publish { get; set; } = true;
    }
}
