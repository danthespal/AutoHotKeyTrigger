namespace OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager.Templates
{
    using OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager.DynamicConditions;
    using OriathHub.Utils;
    using ImGuiNET;

    /// <summary>
    ///     ImGui widget that helps user modify the condition code in <see cref="DynamicCondition"/>.
    /// </summary>
    public static class AilmentTemplate
    {
        private static string statusEffectGroupKey = "Bleeding Or Corruption";

        /// <summary>
        ///     Display the ImGui widget for adding the condition in <see cref="DynamicCondition"/>.
        /// </summary>
        /// <returns>
        ///     condition in string format if user press Add button otherwise empty string.
        /// </returns>
        public static string Add()
        {
            ImGui.Text("Player has");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 11f);
            ImGuiHelper.IEnumerableComboBox(
                    "ailment.##AilmentCondition",
                    JsonDataHelper.StatusEffectGroups.Keys,
                    ref statusEffectGroupKey);
            ImGui.SameLine();
            if (ImGui.Button("Add##AilmentAdd"))
            {
                return $"PlayerAilments.Contains(\"{statusEffectGroupKey}\")";
            }

            return string.Empty;
        }
    }
}
