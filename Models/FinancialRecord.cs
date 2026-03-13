using System;
using System.ComponentModel.DataAnnotations;

namespace FopFinance.Models
{
    /// <summary>
    /// Абстрактний клас фінансового запису — базова сутність для доходів і витрат.
    /// Містить спільні поля та методи валідації.
    /// </summary>
    public abstract class FinancialRecord
    {
        /// <summary>Унікальний ідентифікатор запису (GUID).</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Дата операції.</summary>
        public DateTime Date { get; set; }

        /// <summary>Сума операції (> 0).</summary>
        public decimal Amount { get; set; }

        /// <summary>Опис / коментар до операції.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Базова валідація: сума має бути більшою за нуль,
        /// дата не може бути в майбутньому.
        /// </summary>
        /// <returns>Порожній рядок якщо все ОК, інакше — текст помилки.</returns>
        public virtual string Validate()
        {
            if (Amount <= 0)
                return "Сума повинна бути більшою за нуль.";

            if (Date == default)
                return "Дата не може бути порожньою.";

            if (Date > DateTime.Today.AddDays(1))
                return "Дата не може бути в майбутньому.";

            return string.Empty; // валідація пройдена
        }

        /// <summary>Повертає суму операції (поліморфний аксесор).</summary>
        public virtual decimal GetAmount() => Amount;
    }
}
