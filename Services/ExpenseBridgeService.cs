using System;
using System.Text.Json;
using FopFinance.Managers;
using FopFinance.Models;
using FopFinance.UnitOfWork;

namespace FopFinance.Services
{
    /// <summary>
    /// Bridge service responsible for expense CRUD operations.
    /// </summary>
    public class ExpenseBridgeService
    {
        private readonly ExpenseManager _manager;
        private readonly IUnitOfWork _unitOfWork;

        public ExpenseBridgeService(ExpenseManager manager, IUnitOfWork unitOfWork)
        {
            _manager = manager;
            _unitOfWork = unitOfWork;
        }

        public string GetExpenses() =>
            JsonSerializer.Serialize(_manager.GetAll(), BridgeHelpers.JsonOpts);

        public string AddExpense(string json)
        {
            try
            {
                var expense = BridgeHelpers.Deserialize<Expense>(json);
                if (expense == null) return BridgeHelpers.Error("Некоректні дані.");

                string err = _manager.Add(expense);
                if (!string.IsNullOrEmpty(err)) return BridgeHelpers.Error(err);

                _unitOfWork.Commit();
                return BridgeHelpers.Ok(expense.Id);
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }

        public string UpdateExpense(string json)
        {
            try
            {
                var expense = BridgeHelpers.Deserialize<Expense>(json);
                if (expense == null) return BridgeHelpers.Error("Некоректні дані.");

                string err = _manager.Update(expense);
                if (!string.IsNullOrEmpty(err)) return BridgeHelpers.Error(err);

                _unitOfWork.Commit();
                return BridgeHelpers.Ok();
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }

        public string RemoveExpense(string id)
        {
            bool ok = _manager.Remove(id);
            if (ok) _unitOfWork.Commit();
            return ok ? BridgeHelpers.Ok() : BridgeHelpers.Error("Запис не знайдено.");
        }
    }
}
