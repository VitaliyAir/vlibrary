namespace VLibrary.Infrastructure.Storage
{
    /// <summary>
    /// Access to VStorageAPI v2 (section "VStorage"). ClientSecret is a secret: user-secrets / environment only.
    /// </summary>
    public sealed class VStorageOptions
    {
        public const string SectionName = "VStorage";

        /// <summary>VStorage API, e.g. http://localhost:5190.</summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>The user's library bucket; found by name, created on the first upload.</summary>
        public string BucketName { get; set; } = "vlibrary-books";

        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>Keycloak token endpoint for the token exchange (RFC 8693); empty = from Authentication:Authority.</summary>
        public string TokenEndpoint { get; set; } = string.Empty;

        /// <summary>The confidential Keycloak client of this API that performs the token exchange.</summary>
        public string ClientId { get; set; } = "vlibrary-api";

        /// <summary>Secret of <see cref="ClientId"/> — user-secrets / environment only.</summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>The aud the exchanged token must get.</summary>
        public string Audience { get; set; } = "vstorage";

        public string Scope { get; set; } = "vstorage-audience";

        /// <summary>Lifetime of the links that open a book; null = VStorage default.</summary>
        public int? DownloadLinkSeconds { get; set; } = 300;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(BaseUrl)
            && !string.IsNullOrWhiteSpace(BucketName)
            && !string.IsNullOrWhiteSpace(TokenEndpoint)
            && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret);
    }
}
