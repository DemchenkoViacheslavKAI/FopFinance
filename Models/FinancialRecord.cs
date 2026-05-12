using System;

namespace FopFinance.Models
{
    /// <summary>
    /// Абстрактний клас фінансового запису — базова сутність для доходів і витрат.
    /// Містить спільні поля базового фінансового запису.
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

        /// <summary>Повертає суму операції (поліморфний аксесор).</summary>
        public virtual decimal GetAmount() => Amount;
    }
}
