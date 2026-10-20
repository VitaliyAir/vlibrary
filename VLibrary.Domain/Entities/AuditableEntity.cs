namespace VLibrary.Domain.Entities
{
    /// <summary>
    /// Базовый класс для сущностей, поддерживающих аудит времени создания и обновления.
    /// </summary>
    public abstract class AuditableEntity
    {
        /// <summary>
        /// Уникальный идентификатор сущности (генерируется при создании).
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Дата и время создания записи (UTC). Заполняется при сохранении.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Дата и время последнего обновления записи (UTC). Заполняется при сохранении.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}
