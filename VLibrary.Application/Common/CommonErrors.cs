using VLibrary.Domain.Common;

namespace VLibrary.Application.Common
{
    public static class CommonErrors
    {
        public static Error NotFound(string entity, long id) =>
            Error.NotFound($"{entity}.NotFound", $"{entity} {id} was not found.");

        public static Error InUse(string entity, long id, string reason) =>
            Error.Conflict($"{entity}.InUse", $"{entity} {id} cannot be deleted: {reason}.");
    }
}
