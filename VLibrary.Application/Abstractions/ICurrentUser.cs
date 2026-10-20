namespace VLibrary.Application.Abstractions
{
    /// <summary>
    /// The signed-in user of the current request, taken from the Keycloak access token.
    /// </summary>
    public interface ICurrentUser
    {
        /// <summary>
        /// preferred_username of the access token (falls back to sub); null outside a user request
        /// (startup work, tools).
        /// </summary>
        string? UserName { get; }

        /// <summary>The sub claim (Keycloak user id); null outside a user request.</summary>
        string? UserId { get; }

        /// <summary>The raw bearer token of the request, exchanged for a VStorage token; null outside a user request.</summary>
        string? AccessToken { get; }
    }
}
