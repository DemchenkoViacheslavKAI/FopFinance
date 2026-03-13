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

        /// <summary>
        /// Розширена валідація: перевіряє базові поля + джерело.
        /// </summary>
        public override string Validate()
        {
            string baseError = base.Validate();
            if (!string.IsNullOrEmpty(baseError)) return baseError;

            if (string.IsNullOrWhiteSpace(Source))
                return "Джерело доходу є обов'язковим полем.";

            return string.Empty;
        }
    }
}
