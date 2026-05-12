using System;
using System.Text.Json;
using FopFinance.Managers;
using FopFinance.Models;
using FopFinance.UnitOfWork;

namespace FopFinance.Services
{
    /// <summary>
    /// Bridge service responsible for category CRUD operations.
    /// </summary>
    public class CategoryBridgeService
    {
        private readonly CategoryManager _manager;
        private readonly IUnitOfWork _unitOfWork;

        public CategoryBridgeService(CategoryManager manager, IUnitOfWork unitOfWork)
        {
            _manager = manager;
            _unitOfWork = unitOfWork;
        }

        public string GetCategories() =>
            JsonSerializer.Serialize(_manager.GetAll(), BridgeHelpers.JsonOpts);

        public string AddCategory(string json)
        {
            try
            {
                var cat = BridgeHelpers.Deserialize<Category>(json);
                if (cat == null) return BridgeHelpers.Error("Некоректні дані.");

                string err = _manager.Add(cat);
                if (!string.IsNullOrEmpty(err)) return BridgeHelpers.Error(err);

                _unitOfWork.Commit();
                return BridgeHelpers.Ok(cat.Id);
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }

        public string UpdateCategory(string json)
        {
            try
            {
                var cat = BridgeHelpers.Deserialize<Category>(json);
                if (cat == null) return BridgeHelpers.Error("Некоректні дані.");

                string err = _manager.Update(cat);
                if (!string.IsNullOrEmpty(err)) return BridgeHelpers.Error(err);

                _unitOfWork.Commit();
                return BridgeHelpers.Ok();
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }

        public string RemoveCategory(string id)
        {
            string err = _manager.Remove(id);
            if (string.IsNullOrEmpty(err)) _unitOfWork.Commit();
            return string.IsNullOrEmpty(err) ? BridgeHelpers.Ok() : BridgeHelpers.Error(err);
        }
    }
}
