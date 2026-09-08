using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace STS2MultiplayerLimitBreak.Rooms
{
    internal sealed partial class RoomJoinActionButton : NJoinFriendRefreshButton
    {
        private string _title = "";
        protected override string[] Hotkeys => [];

        public static RoomJoinActionButton Create(NJoinFriendRefreshButton template, string name, int side, string title, Action action)
        {
            var shift = side * (template.OffsetRight - template.OffsetLeft + 16);
            var button = new RoomJoinActionButton
            {
                Name = name,
                _title = title,
                Material = (Material)template.Material.Duplicate(),
                SelfModulate = template.SelfModulate,
                AnchorLeft = template.AnchorLeft,
                AnchorRight = template.AnchorRight,
                AnchorTop = template.AnchorTop,
                AnchorBottom = template.AnchorBottom,
                OffsetLeft = template.OffsetLeft + shift,
                OffsetRight = template.OffsetRight + shift,
                OffsetTop = template.OffsetTop,
                OffsetBottom = template.OffsetBottom,
                GrowHorizontal = template.GrowHorizontal,
                GrowVertical = template.GrowVertical,
                PivotOffset = template.PivotOffset,
                FocusMode = FocusModeEnum.All,
            };
            button.AddChild(template.GetNode("TextureRect").Duplicate((int)DuplicateFlags.Scripts));
            button.AddChild(template.GetNode("Label").Duplicate((int)DuplicateFlags.Scripts));
            button.Connect(NClickableControl.SignalName.Released, Callable.From<NClickableControl>(_ => action()));
            return button;
        }

        public override void _Ready()
        {
            base._Ready();
            GetNode<MegaLabel>("Label").SetTextAutoSize(_title);
        }
    }
}
