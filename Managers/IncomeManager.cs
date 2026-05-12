using System;
using System.Collections.Generic;
using System.Linq;
using FopFinance.Models;
using FopFinance.Repositories;
using FopFinance.Validators;

namespace FopFinance.Managers
{
    /// <summary>
    /// Handles all business logic for income records.
    /// </summary>
    public class IncomeManager
    {
        private readonly IIncomeRepository _repository;

        public IncomeManager(IIncomeRepository repository)
        {
            _repository = repository;
        }

        public IReadOnlyList<Income> GetAll() => _repository.GetAll();

        public string Add(Income income)
        {
            string error = IncomeValidator.Validate(income);
            if (!string.IsNullOrEmpty(error)) return error;

            _repository.Add(income);
            return string.Empty;
        }

        public string Update(Income updated)
        {
            string error = IncomeValidator.Validate(updated);
            if (!string.IsNullOrEmpty(error)) return error;

            return _repository.Update(updated)
                ? string.Empty
                : "Запис не знайдено.";
        }

        public bool Remove(string id) => _repository.Remove(id);

        public decimal CalculateTotal(DateTime? from = null, DateTime? to = null)
        {
            var items = from.HasValue
                ? _repository.GetAll().Where(i => i.Date >= from && i.Date <= to)
                : _repository.GetAll();
            return items.Sum(i => i.Amount);
        }

        public List<Income> GetByPeriod(DateTime start, DateTime end) =>
            _repository.GetAll()
                .Where(i => i.Date >= start && i.Date <= end)
                .ToList();
    }
}
