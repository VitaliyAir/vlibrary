using VLibrary.Domain.Rules;

namespace VLibrary.Domain.Entities
{
    public class TagEntity : AuditableEntity
    {
        public string Name { get; private set; } = string.Empty;
        public string NormalizedName { get; private set; } = string.Empty;

        public virtual ICollection<BookEntity> Books { get; set; } = new List<BookEntity>();

        public static TagEntity Create(string name)
        {
            var trimmed = name.Trim();
            return new TagEntity
            {
                Name = trimmed,
                NormalizedName = TextNormalizer.Normalize(trimmed)
            };
        }
    }
}
