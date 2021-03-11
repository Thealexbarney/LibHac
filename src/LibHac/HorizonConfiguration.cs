#nullable enable
using LibHac.Os;

namespace LibHac
{
    /// <summary>
    /// Contains configuration options for instantiating a <see cref="Horizon"/> object.
    /// </summary>
    public class HorizonConfiguration
    {
        /// <summary>
        /// Used when getting the current system <see cref="Tick"/>.
        /// If <see langword="null"/>, a default <see cref="ITickGenerator"/> is used.
        /// </summary>
        public ITickGenerator? TickGenerator { get; set; }
    }
}
