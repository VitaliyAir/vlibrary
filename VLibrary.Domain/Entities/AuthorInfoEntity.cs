using VLibrary.Domain.ValueObjects;

namespace VLibrary.Domain.Entities
{
    public class AuthorInfoEntity : AuditableEntity
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? MiddleName { get; set; }
        public string? NameInitials { get; set; }
        public string? Biography { get; set; }

        /// <summary>
        /// Matching key: normalized last name + first initial (see <see cref="PersonName.MatchKey"/>).
        /// </summary>
        public string NormalizedName { get; private set; } = string.Empty;

        public virtual ICollection<BookAuthorEntity> BookAuthors { get; set; } = new List<BookAuthorEntity>();

        public string DisplayName => PersonName.Format(LastName, FirstName, MiddleName, NameInitials);

        public static AuthorInfoEntity Create(PersonName name)
        {
            var author = new AuthorInfoEntity
            {
                FirstName = name.FirstName,
                MiddleName = name.MiddleName,
                LastName = name.LastName,
                NameInitials = name.Initials
            };
            author.RefreshNormalizedName();
            return author;
        }

        /// <summary>
        /// The same author (same match key) written more fully, e.g. "Толстой, Лев Николаевич" after "Толстой Л.Н.":
        /// missing first / middle names are filled in; existing parts are not changed.
        /// </summary>
        public void Complete(PersonName name)
        {
            if (string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(name.FirstName))
            {
                FirstName = name.FirstName;
                MiddleName ??= name.MiddleName;
            }
            else if (string.IsNullOrWhiteSpace(MiddleName) && !string.IsNullOrWhiteSpace(name.MiddleName)
                     && string.Equals(FirstName, name.FirstName, StringComparison.OrdinalIgnoreCase))
            {
                MiddleName = name.MiddleName;
            }
        }

        public void RefreshNormalizedName()
        {
            NormalizedName = PersonName.BuildMatchKey(LastName, FirstName, NameInitials);
        }
    }
}
