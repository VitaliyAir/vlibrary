namespace VLibrary.Api.Auth
{
    public static class AppRoles
    {
        /// <summary>Client role in Keycloak: full access to every API action (Keycloak admin calls are a separate admin role).</summary>
        public const string Owner = "Owner";
    }
}
