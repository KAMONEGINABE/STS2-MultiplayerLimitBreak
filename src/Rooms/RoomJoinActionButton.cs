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
            if (template.GetNodeOrNull<Control>("TextureRect") is { } texture)
                button.AddChild(texture.Duplicate((int)DuplicateFlags.Scripts));
            else if (template.IsClass("NinePatchRect"))
            {
                var background = new NinePatchRect
                {
                    Name = "Background",
                    SelfModulate = template.SelfModulate,
                    UseParentMaterial = true,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                foreach (var property in new[]
                         {
                             "texture", "region_rect", "draw_center", "patch_margin_left", "patch_margin_top",
                             "patch_margin_right", "patch_margin_bottom", "axis_stretch_horizontal", "axis_stretch_vertical",
                         })
                    background.Set(property, template.Get(property));
                button.AddChild(background);
                background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            }
            else
                throw new InvalidOperationException("Unsupported refresh button background.");
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
