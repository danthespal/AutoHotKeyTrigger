namespace OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager.Templates
{
    using OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager.DynamicConditions;
    using ImGuiNET;

    /// <summary>
    ///     ImGui widget that helps user modify the condition code in <see cref="DynamicCondition"/>.
    /// </summary>
    public static class ShapeShiftedTemplate
    {
        private static bool yesOrNo = true;

        /// <summary>
        ///     Display the ImGui widget for adding the condition in <see cref="DynamicCondition"/>.
        /// </summary>
        /// <returns>
        ///     condition in string format if user press Add button otherwise empty string.
        /// </returns>
        public static string Add()
        {
            ImGui.Checkbox("Player is shape shifted to daemon/wolf/etc.", ref yesOrNo);
            if (ImGui.Button("Add##ShapeShifted"))
            {
                return yesOrNo ? "PlayerIsShapeShifted" : "!PlayerIsShapeShifted";
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
