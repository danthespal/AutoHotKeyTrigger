namespace OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager.Templates
{
    using OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager.DynamicConditions;
    using ClickableTransparentOverlay.Win32;
    using OriathHub.Utils;
    using ImGuiNET;

    /// <summary>
    ///     ImGui widget that helps user modify the condition code in <see cref="DynamicCondition"/>.
    /// </summary>
    public static class IsKeyPressedTemplate
    {
        private static VK pressedKey = VK.KEY_A;

        /// <summary>
        ///     Display the ImGui widget for adding the condition in <see cref="DynamicCondition"/>.
        /// </summary>
        /// <returns>
        ///     condition in string format if user press Add button otherwise empty string.
        /// </returns>
        public static string Add()
        {
            ImGui.Text("User has pressed");
            ImGui.SameLine();
            ImGuiHelper.NonContinuousEnumComboBox("Key##IsKeyPressedTemplate", ref pressedKey);
            if (ImGui.Button("Add##IsKeyPressed"))
            {
                return $"IsKeyPressedForAction(VK.{pressedKey})";
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
