using System;

namespace FopFinance.Models
{
    /// <summary>
    /// Опис профілю ФОП для мультипрофільного режиму.
    /// </summary>
    public class Profile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
