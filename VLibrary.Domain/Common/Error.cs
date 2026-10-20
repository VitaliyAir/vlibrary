namespace VLibrary.Domain.Common
{
    public enum ErrorType
    {
        Failure = 0,
        Validation = 1,
        NotFound = 2,
        Conflict = 3,

        /// <summary>A service the request needs (VStorage, Keycloak) is not reachable: retry later.</summary>
        Unavailable = 4
    }

    public sealed record Error(
        string Code,
        string Message,
        ErrorType Type = ErrorType.Failure,
        IReadOnlyDictionary<string, string[]>? Details = null)
    {
        public static readonly Error None = new(string.Empty, string.Empty);

        public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
        public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
        public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
        public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
        public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);

        public static Error ValidationFailed(IReadOnlyDictionary<string, string[]> details) =>
            new("Validation.Failed", "One or more validation errors occurred.", ErrorType.Validation, details);
    }
}
