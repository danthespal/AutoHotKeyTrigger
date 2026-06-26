namespace OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager.DynamicConditions.Interface
{
    /// <summary>
    ///     Information about a vital
    /// </summary>
    public interface IVital
    {
        /// <summary>
        ///     Current value
        /// </summary>
        int Current { get; }

        /// <summary>
        ///     Maximum value
        /// </summary>
        int Max { get; }

        /// <summary>
        ///     Current Value in percent from the max
        /// </summary>
        int Percent { get; }

        /// <summary>
        ///     Reserved Value in percent from the max
        /// </summary>
        int Reserved { get; }
    }
}
