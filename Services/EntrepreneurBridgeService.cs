using System;
using System.Text.Json;
using FopFinance.Models;
using FopFinance.Repositories;
using FopFinance.UnitOfWork;

namespace FopFinance.Services
{
    /// <summary>
    /// Bridge service responsible for entrepreneur (FOP profile) data.
    /// </summary>
    public class EntrepreneurBridgeService
    {
        private readonly IEntrepreneurRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public EntrepreneurBridgeService(IEntrepreneurRepository repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public string GetEntrepreneur() =>
            JsonSerializer.Serialize(_repository.Get(), BridgeHelpers.JsonOpts);

        public string SaveEntrepreneur(string json)
        {
            try
            {
                var e = BridgeHelpers.Deserialize<Entrepreneur>(json);
                if (e == null) return BridgeHelpers.Error("Некоректні дані профілю.");

                if (string.IsNullOrWhiteSpace(e.FullName))
                    return BridgeHelpers.Error("ПІБ є обов'язковим.");

                if (e.TaxGroup < 1 || e.TaxGroup > 3)
                    return BridgeHelpers.Error("Група ЄП повинна бути в діапазоні 1..3.");

                _repository.Set(e);
                _unitOfWork.Commit();
                return BridgeHelpers.Ok();
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }
    }
}
