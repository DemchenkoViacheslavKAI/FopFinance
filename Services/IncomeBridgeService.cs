using System;
using System.Text.Json;
using FopFinance.Managers;
using FopFinance.Models;
using FopFinance.UnitOfWork;

namespace FopFinance.Services
{
    /// <summary>
    /// Bridge service responsible for income CRUD operations.
    /// </summary>
    public class IncomeBridgeService
    {
        private readonly IncomeManager _manager;
        private readonly IUnitOfWork _unitOfWork;

        public IncomeBridgeService(IncomeManager manager, IUnitOfWork unitOfWork)
        {
            _manager = manager;
            _unitOfWork = unitOfWork;
        }

        public string GetIncomes() =>
            JsonSerializer.Serialize(_manager.GetAll(), BridgeHelpers.JsonOpts);

        public string AddIncome(string json)
        {
            try
            {
                var income = BridgeHelpers.Deserialize<Income>(json);
                if (income == null) return BridgeHelpers.Error("Некоректні дані.");

                string err = _manager.Add(income);
                if (!string.IsNullOrEmpty(err)) return BridgeHelpers.Error(err);

                _unitOfWork.Commit();
                return BridgeHelpers.Ok(income.Id);
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }

        public string UpdateIncome(string json)
        {
            try
            {
                var income = BridgeHelpers.Deserialize<Income>(json);
                if (income == null) return BridgeHelpers.Error("Некоректні дані.");

                string err = _manager.Update(income);
                if (!string.IsNullOrEmpty(err)) return BridgeHelpers.Error(err);

                _unitOfWork.Commit();
                return BridgeHelpers.Ok();
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }

        public string RemoveIncome(string id)
        {
            bool ok = _manager.Remove(id);
            if (ok) _unitOfWork.Commit();
            return ok ? BridgeHelpers.Ok() : BridgeHelpers.Error("Запис не знайдено.");
        }
    }
}
