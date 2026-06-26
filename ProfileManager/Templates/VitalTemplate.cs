namespace OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager.Templates
{
    using OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager.DynamicConditions;
    using OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager.Enums;
    using OriathHub.Utils;
    using ImGuiNET;
    using System.Collections.Generic;

    /// <summary>
    ///     ImGui widget that helps user modify the condition code in <see cref="DynamicCondition"/>.
    /// </summary>
    public static class VitalTemplate
    {
        private static readonly List<string> SupportedOperatorTypes = new()
        {
            ">",
            ">=",
            "<",
            "<="
        };

        private static VitalType vitalType  = VitalType.HP_PERCENT;
        private static string selectedOperator = "<=";
        private static int threshold = 90;

        /// <summary>
        ///     Display the ImGui widget for adding the condition in <see cref="DynamicCondition"/>.
        /// </summary>
        /// <returns>
        ///     condition in string format if user press Add button otherwise empty string.
        /// </returns>
        public static string Add()
        {
            ImGui.Text("Player");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 8);
            ImGuiHelper.EnumComboBox("is##VitalSelector", ref vitalType);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 3);
            ImGuiHelper.IEnumerableComboBox("##VitalOperator", SupportedOperatorTypes, ref selectedOperator);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 5);
            ImGui.InputInt("##VitalThreshold", ref threshold);
            ImGui.SameLine();
            if (ImGui.Button("Add##Vital"))
            {
                return $"PlayerVitals.{vitalType.ToString().Replace("_", ".")} {selectedOperator} {threshold}";
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
