using System;

namespace FopFinance.Models
{
    /// <summary>
    /// Клас «Дохід» — успадковує FinancialRecord.
    /// Представляє одну прибуткову операцію ФОП.
    /// </summary>
    public class Income : FinancialRecord
    {
        /// <summary>Джерело доходу (клієнт, контракт тощо).</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Розраховує суму єдиного податку залежно від групи ФОП.
        /// За замовчуванням — 5 % (спрощена система, 3 група).
        /// </summary>
        /// <param name="taxRatePercent">Відсоток податку (за замовчуванням 5).</param>
        /// <returns>Сума податку.</returns>
        public decimal CalculateTax(decimal taxRatePercent = 5m)
        {
            return Math.Round(Amount * taxRatePercent / 100m, 2);
        }
    }
}
