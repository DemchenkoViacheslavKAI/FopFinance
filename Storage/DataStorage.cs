using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using FopFinance.Models;

namespace FopFinance.Storage
{
    /// <summary>
    /// Клас збереження та завантаження даних.
    /// Підтримує формати JSON і XML.
    /// Усі дані зберігаються в одному файлі як єдиний документ.
    /// </summary>
    public static class DataStorage
    {
        private const int CurrentSchemaVersion = 2;

        // --- внутрішній DTO для серіалізації всіх колекцій разом ---
        private class AppData
        {
            public int SchemaVersion { get; set; } = CurrentSchemaVersion;
            public Entrepreneur Entrepreneur { get; set; } = new();
            public List<Income>   Incomes    { get; set; } = new();
            public List<Expense>  Expenses   { get; set; } = new();
            public List<Category> Categories { get; set; } = new();
            public List<Profile>  Profiles   { get; set; } = new();
            public string ActiveProfileId   { get; set; } = string.Empty;
        }

        // ===================== JSON =====================

        /// <summary>Зберігає всі дані у файл JSON.</summary>
        public static void SaveToJSON(string path,
            Entrepreneur entrepreneur,
            List<Income> incomes,
            List<Expense> expenses,
            List<Category> categories,
            List<Profile>? profiles = null,
            string activeProfileId = "")
        {
            var data = new AppData
            {
                SchemaVersion = CurrentSchemaVersion,
                Entrepreneur = entrepreneur,
                Incomes      = incomes,
                Expenses     = expenses,
                Categories   = categories,
                Profiles     = profiles ?? new List<Profile>(),
                ActiveProfileId = activeProfileId ?? string.Empty
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(path, json);
        }

        /// <summary>Завантажує дані з файлу JSON.</summary>
        public static (Entrepreneur, List<Income>, List<Expense>, List<Category>, List<Profile>, string)
            LoadFromJSON(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл не знайдено: {path}");

            string json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<AppData>(json, options)
                       ?? throw new InvalidDataException("Не вдалося прочитати дані з JSON.");

            if (data.SchemaVersion <= 0)
                data.SchemaVersion = 1;

            return (data.Entrepreneur, data.Incomes, data.Expenses, data.Categories, data.Profiles, data.ActiveProfileId);
        }

        // ===================== XML =====================

        /// <summary>Зберігає всі дані у файл XML.</summary>
        public static void SaveToXML(string path,
            Entrepreneur entrepreneur,
            List<Income> incomes,
            List<Expense> expenses,
            List<Category> categories,
            List<Profile>? profiles = null,
            string activeProfileId = "")
        {
            var root = new XElement("AppData",
                new XAttribute("schemaVersion", CurrentSchemaVersion),
                SerializeEntrepreneur(entrepreneur),
                new XElement("Incomes",    SerializeIncomes(incomes)),
                new XElement("Expenses",   SerializeExpenses(expenses)),
                new XElement("Categories", SerializeCategories(categories)),
                SerializeProfiles(profiles ?? new List<Profile>()),
                new XElement("ActiveProfileId", activeProfileId ?? string.Empty));

            new XDocument(new XDeclaration("1.0", "utf-8", null), root)
                .Save(path);
        }

        /// <summary>Завантажує дані з файлу XML.</summary>
        public static (Entrepreneur, List<Income>, List<Expense>, List<Category>, List<Profile>, string)
            LoadFromXML(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл не знайдено: {path}");

            var doc = XDocument.Load(path);
            var root = doc.Root ?? throw new InvalidDataException("Порожній XML-документ.");

            _ = int.TryParse(root.Attribute("schemaVersion")?.Value, out int schemaVersion)
                ? schemaVersion
                : 1;

            var entrepreneur = DeserializeEntrepreneur(root.Element("Entrepreneur"));
            var incomes      = DeserializeIncomes   (root.Element("Incomes"));
            var expenses     = DeserializeExpenses  (root.Element("Expenses"));
            var categories   = DeserializeCategories(root.Element("Categories"));
            var profiles     = DeserializeProfiles   (root.Element("Profiles"));
            var activeProfileId = root.Element("ActiveProfileId")?.Value ?? string.Empty;

            return (entrepreneur, incomes, expenses, categories, profiles, activeProfileId);
        }

        // ===================== Сериалізація XML (приватні методи) =====================

        private static XElement SerializeEntrepreneur(Entrepreneur e) =>
            new("Entrepreneur",
                new XElement("Id", e.Id),
                new XElement("FullName", e.FullName),
                new XElement("TaxGroup", e.TaxGroup),
                new XElement("RegistrationNumber", e.RegistrationNumber));

        private static List<XElement> SerializeIncomes(List<Income> list)
        {
            var result = new List<XElement>();
            foreach (var i in list)
                result.Add(new XElement("Income",
                    new XElement("Id", i.Id),
                    new XElement("Date", i.Date.ToString("yyyy-MM-dd")),
                    new XElement("Amount", i.Amount),
                    new XElement("Source", i.Source),
                    new XElement("Description", i.Description)));
            return result;
        }

        private static List<XElement> SerializeExpenses(List<Expense> list)
        {
            var result = new List<XElement>();
            foreach (var e in list)
                result.Add(new XElement("Expense",
                    new XElement("Id", e.Id),
                    new XElement("Date", e.Date.ToString("yyyy-MM-dd")),
                    new XElement("Amount", e.Amount),
                    new XElement("CategoryId", e.CategoryId),
                    new XElement("CategoryName", e.CategoryName),
                    new XElement("Description", e.Description)));
            return result;
        }

        private static List<XElement> SerializeCategories(List<Category> list)
        {
            var result = new List<XElement>();
            foreach (var c in list)
                result.Add(new XElement("Category",
                    new XElement("Id", c.Id),
                    new XElement("Name", c.Name),
                    new XElement("Description", c.Description)));
            return result;
        }

        private static XElement SerializeProfiles(List<Profile> list)
        {
            var root = new XElement("Profiles");
            foreach (var p in list)
            {
                root.Add(new XElement("Profile",
                    new XElement("Id", p.Id),
                    new XElement("Name", p.Name),
                    new XElement("CreatedAt", p.CreatedAt.ToString("o"))));
            }

            return root;
        }

        // ===================== Десеріалізація XML (приватні методи) =====================

        private static Entrepreneur DeserializeEntrepreneur(XElement? el)
        {
            if (el == null) return new Entrepreneur();
            return new Entrepreneur
            {
                Id                 = el.Element("Id")?.Value ?? Guid.NewGuid().ToString(),
                FullName           = el.Element("FullName")?.Value ?? "",
                TaxGroup           = int.TryParse(el.Element("TaxGroup")?.Value, out int tg) ? tg : 3,
                RegistrationNumber = el.Element("RegistrationNumber")?.Value ?? ""
            };
        }

        private static List<Income> DeserializeIncomes(XElement? el)
        {
            var list = new List<Income>();
            if (el == null) return list;
            foreach (var xe in el.Elements("Income"))
                list.Add(new Income
                {
                    Id          = xe.Element("Id")?.Value ?? Guid.NewGuid().ToString(),
                    Date        = DateTime.TryParse(xe.Element("Date")?.Value, out DateTime d) ? d : DateTime.Today,
                    Amount      = decimal.TryParse(xe.Element("Amount")?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var a) ? a : 0m,
                    Source      = xe.Element("Source")?.Value ?? "",
                    Description = xe.Element("Description")?.Value ?? ""
                });
            return list;
        }

        private static List<Expense> DeserializeExpenses(XElement? el)
        {
            var list = new List<Expense>();
            if (el == null) return list;
            foreach (var xe in el.Elements("Expense"))
                list.Add(new Expense
                {
                    Id           = xe.Element("Id")?.Value ?? Guid.NewGuid().ToString(),
                    Date         = DateTime.TryParse(xe.Element("Date")?.Value, out DateTime d) ? d : DateTime.Today,
                    Amount       = decimal.TryParse(xe.Element("Amount")?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var a) ? a : 0m,
                    CategoryId   = xe.Element("CategoryId")?.Value ?? "",
                    CategoryName = xe.Element("CategoryName")?.Value ?? "",
                    Description  = xe.Element("Description")?.Value ?? ""
                });
            return list;
        }

        private static List<Category> DeserializeCategories(XElement? el)
        {
            var list = new List<Category>();
            if (el == null) return list;
            foreach (var xe in el.Elements("Category"))
                list.Add(new Category
                {
                    Id          = xe.Element("Id")?.Value ?? Guid.NewGuid().ToString(),
                    Name        = xe.Element("Name")?.Value ?? "",
                    Description = xe.Element("Description")?.Value ?? ""
                });
            return list;
        }

        private static List<Profile> DeserializeProfiles(XElement? el)
        {
            var list = new List<Profile>();
            if (el == null) return list;

            foreach (var xe in el.Elements("Profile"))
            {
                list.Add(new Profile
                {
                    Id = xe.Element("Id")?.Value ?? Guid.NewGuid().ToString(),
                    Name = xe.Element("Name")?.Value ?? string.Empty,
                    CreatedAt = DateTime.TryParse(xe.Element("CreatedAt")?.Value, out DateTime createdAt)
                        ? createdAt
                        : DateTime.UtcNow
                });
            }

            return list;
        }
    }
}
