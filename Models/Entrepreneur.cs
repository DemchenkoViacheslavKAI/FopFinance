using System;

namespace FopFinance.Models
{
    /// <summary>
    /// Клас «ФОП» — представляє підприємця.
    /// Зберігає ідентифікаційні дані та генерує короткий підсумок.
    /// </summary>
    public class Entrepreneur
    {
        /// <summary>Унікальний ідентифікатор.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>ПІБ підприємця.</summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>Група єдиного податку (1, 2 або 3).</summary>
        public int TaxGroup { get; set; } = 3;

        /// <summary>РНОКПП (ІПН) або ЄДРПОУ.</summary>
        public string RegistrationNumber { get; set; } = string.Empty;

        /// <summary>
        /// Повертає рядковий підсумок — для відображення у шапці UI.
        /// </summary>
        public string GetFinancialSummary()
        {
            return $"ФОП: {FullName} | Група: {TaxGroup} | РНОКПП: {RegistrationNumber}";
        }
    }
}
