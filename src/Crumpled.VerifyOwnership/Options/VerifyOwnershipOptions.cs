namespace Crumpled.VerifyOwnership.Options
{
    /// <summary>
    /// Bound from <c>Crumpled:VerifyOwnership</c>. Package-wide settings not specific to a single provider.
    /// </summary>
    public class VerifyOwnershipOptions
    {
        /// <summary>
        /// When <c>true</c> and an <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
        /// is registered, "last requested" tracking uses it instead of sharing the key/value store's entries
        /// document. Has no effect on where verification entries themselves are stored - that's always the
        /// key/value store, regardless of this setting. Defaults to <c>false</c>.
        /// </summary>
        public bool UseDistributedCache { get; set; }
    }
}
