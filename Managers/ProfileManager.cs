using System;
using System.Collections.Generic;
using FopFinance.Facade;
using FopFinance.Models;
using FopFinance.Repositories;
using FopFinance.UnitOfWork;

namespace FopFinance.Managers
{
    /// <summary>
    /// Handles all business logic for multi-profile management.
    /// </summary>
    public class ProfileManager
    {
        private readonly IProfileRepository _repository;
        private readonly FinanceStorageFacade _storage;
        private readonly IUnitOfWork _unitOfWork;

        public string ActiveProfileId { get; private set; } = string.Empty;

        public ProfileManager(
            IProfileRepository repository,
            FinanceStorageFacade storage,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _storage = storage;
            _unitOfWork = unitOfWork;
        }

        public IReadOnlyList<Profile> GetAll() => _repository.GetAll();

        public void Initialize(List<Profile> profiles, string activeProfileId)
        {
            _repository.ReplaceAll(profiles);
            ActiveProfileId = ResolveActiveProfileId(profiles, activeProfileId);
            _unitOfWork.SetActiveProfile(ActiveProfileId);
        }

        public (string error, string profileId) Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return ("Назва профілю не може бути порожньою.", string.Empty);

            bool exists = false;
            foreach (var p in _repository.GetAll())
            {
                if (p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

            if (exists)
                return ("Профіль з такою назвою вже існує.", string.Empty);

            var profile = _storage.CreateProfile(name.Trim());
            _repository.Add(profile);
            return (string.Empty, profile.Id);
        }

        public bool Switch(string profileId)
        {
            if (!_repository.Exists(profileId)) return false;
            ActiveProfileId = profileId;
            _unitOfWork.SetActiveProfile(profileId);
            return true;
        }

        private static string ResolveActiveProfileId(List<Profile> profiles, string activeProfileId)
        {
            if (profiles == null || profiles.Count == 0) return activeProfileId ?? string.Empty;

            foreach (var p in profiles)
                if (p.Id == activeProfileId) return activeProfileId;

            return profiles[0].Id;
        }
    }
}
