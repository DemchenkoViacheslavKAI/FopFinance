using System;
using System.Text.Json;
using FopFinance.Managers;
using FopFinance.UnitOfWork;

namespace FopFinance.Services
{
    /// <summary>
    /// Bridge service responsible for multi-profile management.
    /// </summary>
    public class ProfileBridgeService
    {
        private readonly ProfileManager _manager;
        private readonly IUnitOfWork _unitOfWork;

        public ProfileBridgeService(ProfileManager manager, IUnitOfWork unitOfWork)
        {
            _manager = manager;
            _unitOfWork = unitOfWork;
        }

        public string GetProfiles() =>
            JsonSerializer.Serialize(_manager.GetAll(), BridgeHelpers.JsonOpts);

        public string GetActiveProfileId() =>
            BridgeHelpers.Ok(_manager.ActiveProfileId);

        public string AddProfile(string profileName)
        {
            try
            {
                var (error, profileId) = _manager.Create(profileName);
                if (!string.IsNullOrEmpty(error)) return BridgeHelpers.Error(error);

                _manager.Switch(profileId);
                _unitOfWork.Reload();
                return BridgeHelpers.Ok(profileId);
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }

        public string SwitchProfile(string profileId)
        {
            try
            {
                bool switched = _manager.Switch(profileId);
                if (!switched) return BridgeHelpers.Error("Профіль не знайдено.");

                _unitOfWork.Reload();
                return BridgeHelpers.Ok(profileId);
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }
    }
}
