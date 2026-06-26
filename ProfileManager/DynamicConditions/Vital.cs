namespace OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager.DynamicConditions
{
    using OriathHub.RemoteObjects.Components;
    using OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager.DynamicConditions.Interface;

    /// <summary>
    ///     Information about a vital
    /// </summary>
    /// <param name="Current">Current value</param>
    /// <param name="Max">Maximum value</param>
    /// <param name="Percent">Current value in %</param>
    /// <param name="Reserved">Reserved value in %</param>
    public record Vital(int Current, int Max, int Percent, int Reserved) : IVital
    {
        /// <summary>
        ///     Creates a new instance
        /// </summary>
        /// <param name="vital">Source data for the structure</param>
        /// <returns></returns>
        public static Vital From(VitalInfo vital)
        {
            return new Vital(vital.Current, vital.Unreserved, vital.CurrentInPercent(), vital.ReservedInPercent());
        }
    }
}
