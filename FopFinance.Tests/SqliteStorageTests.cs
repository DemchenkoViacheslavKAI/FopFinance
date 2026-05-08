// ════════════════════════════════════════════════════════════════════════
//  FopFinance — Модульні тести: SqliteStorage (допоміжні задачі)
//
//  Чек-лист №2 — SqliteStorage (допоміжні задачі — БД)
//  ──────────────────────────────────────────────────────────────────────
//  SQ-01  EnsureDatabase — файл БД створюється
//  SQ-02  EnsureDatabase — ідемпотентний (повторний виклик не кидає)
//  SQ-03  AddProfile — профіль зберігається в БД
//  SQ-04  AddProfile — порожній рядок обрізається і зберігається
//  SQ-05  GetProfiles — повертає всі збережені профілі
//  SQ-06  SaveProfileData/LoadProfileData — повний round-trip
//  SQ-07  SaveProfileData — Entrepreneur зберігається з коректними полями
//  SQ-08  SaveProfileData — Income зберігається з усіма полями
//  SQ-09  SaveProfileData — Expense зберігається з CategoryName
//  SQ-10  SaveProfileData — Category зберігається
//  SQ-11  LoadProfileData — порожній профіль → порожні списки
//  SQ-12  SaveProfileData повторний виклик → замінює дані (idempotent upsert)
//  SQ-13  GetProfiles — після видалення профілю (через delete cascade) не повертає
// ════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FopFinance.Models;
using FopFinance.Storage;
using Xunit;

namespace FopFinance.Tests;

/// <summary>
/// Базовий клас для SQLite-тестів: кожен тест отримує свій ізольований файл БД.
/// </summary>
public abstract class SqliteTempDbBase : IDisposable
{
    protected string DbPath { get; }

    protected SqliteTempDbBase()
    {
        DbPath = Path.Combine(Path.GetTempPath(), $"foptestdb_{Guid.NewGuid():N}.db");
        SqliteStorage.EnsureDatabase(DbPath);
    }

    public void Dispose()
    {
        // SQLite Microsoft.Data.Sqlite залишає WAL-файли — очищаємо всі
        foreach (var path in new[] { DbPath, DbPath + "-wal", DbPath + "-shm" })
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
        }
    }

    // ── Допоміжні фабрики ─────────────────────────────────────────────

    protected static Entrepreneur SampleEntrepreneur(string name = "Іваненко Іван") =>
        new() { FullName = name, TaxGroup = 2, RegistrationNumber = "2222222222" };

    protected static List<Income> SampleIncomes(int count = 1) =>
        [.. Enumerable.Range(1, count).Select(i =>
            new Income
            {
                Id          = $"inc-{i}",
                Date        = new DateTime(2026, 4, i),
                Amount      = 1000m * i,
                Source      = $"Client-{i}",
                Description = $"Desc-{i}"
            })];

    protected static (List<Expense>, List<Category>) SampleExpensesWithCategory()
    {
        var cat = new Category { Id = "cat-test", Name = "Тестова", Description = "Авто" };
        var expenses = new List<Expense>
        {
            new()
            {
                Id           = "exp-1",
                Date         = new DateTime(2026, 4, 5),
                Amount       = 450m,
                CategoryId   = cat.Id,
                CategoryName = cat.Name,
                Description  = "Авто-витрата"
            }
        };
        return (expenses, new List<Category> { cat });
    }
}

// ═══════════════════════════════════════════════════════════════════════
// Тест-кейс SQ-A: Схема та профілі
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Тест-кейс SQ-A: Ініціалізація БД та управління профілями.
/// Ізоляція: кожен тест — новий файл SQLite на диску.
/// </summary>
public class SqliteStorage_SchemaAndProfileTests : SqliteTempDbBase
{
    // ── SQ-01 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "SQ-01: EnsureDatabase — файл БД створюється на диску")]
    public void EnsureDatabase_CreatesFile()
    {
        Assert.True(File.Exists(DbPath));
    }

    // ── SQ-02 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "SQ-02: EnsureDatabase — повторний виклик не кидає виняток")]
    public void EnsureDatabase_CalledTwice_NoException()
    {
        var ex = Record.Exception(() => SqliteStorage.EnsureDatabase(DbPath));
        Assert.Null(ex);
    }

    // ── SQ-03 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "SQ-03: AddProfile — профіль зберігається в БД")]
    public void AddProfile_StoresProfileInDatabase()
    {
        var profile = SqliteStorage.AddProfile(DbPath, "Основний");

        Assert.NotNull(profile);
        Assert.False(string.IsNullOrEmpty(profile.Id));
        Assert.Equal("Основний", profile.Name);
    }

    // ── SQ-04 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "SQ-04: AddProfile — пробіли на краях назви обрізаються")]
    public void AddProfile_TrimsNameWhitespace()
    {
        var profile = SqliteStorage.AddProfile(DbPath, "  Резервний  ");
        Assert.Equal("Резервний", profile.Name);
    }

    // ── SQ-05 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "SQ-05: GetProfiles — повертає всі додані профілі")]
    public void GetProfiles_ReturnsAllAddedProfiles()
    {
        SqliteStorage.AddProfile(DbPath, "Alpha");
        SqliteStorage.AddProfile(DbPath, "Beta");
        SqliteStorage.AddProfile(DbPath, "Gamma");

        var profiles = SqliteStorage.GetProfiles(DbPath);

        Assert.Equal(3, profiles.Count);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// Тест-кейс SQ-B: SaveProfileData / LoadProfileData round-trips
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Тест-кейс SQ-B: Збереження та завантаження даних профілю.
/// </summary>
public class SqliteStorage_DataRoundTripTests : SqliteTempDbBase
{
    // ── SQ-06 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "SQ-06: SaveProfileData/LoadProfileData — повний round-trip")]
    public void SaveLoad_FullRoundTrip()
    {
        var profile = SqliteStorage.AddProfile(DbPath, "FullRoundTrip");
        var ent = SampleEntrepreneur("Повний Round-Trip ФОП");
        var incomes = SampleIncomes(2);
        var (expenses, categories) = SampleExpensesWithCategory();

        SqliteStorage.SaveProfileData(DbPath, profile.Id, ent, incomes, expenses, categories);
        var (ldEnt, ldInc, ldExp, ldCat) = SqliteStorage.LoadProfileData(DbPath, profile.Id);

        Assert.Equal(ent.FullName,           ldEnt.FullName);
        Assert.Equal(2,                      ldInc.Count);
        Assert.Single(ldExp);
        Assert.Single(ldCat);
    }

    // ── SQ-07 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "SQ-07: SaveProfileData — Entrepreneur зберігається з коректними полями")]
    public void SaveLoad_EntrepreneurFieldsPreserved()
    {
        var profile = SqliteStorage.AddProfile(DbPath, "EntrepreneurTest");
        var ent = new Entrepreneur
        {
            FullName           = "Леся Українка",
            TaxGroup           = 1,
            RegistrationNumber = "9998887770"
        };
        SqliteStorage.SaveProfileData(DbPath, profile.Id, ent, [], [], []);
        var (loaded, _, _, _) = SqliteStorage.LoadProfileData(DbPath, profile.Id);

        Assert.Equal("Леся Українка", loaded.FullName);
        Assert.Equal(1,               loaded.TaxGroup);
        Assert.Equal("9998887770",    loaded.RegistrationNumber);
    }

    // ── SQ-08 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "SQ-08: SaveProfileData — Income зберігається з усіма полями")]
    public void SaveLoad_IncomeFieldsPreserved()
    {
        var profile = SqliteStorage.AddProfile(DbPath, "IncomeFieldTest");
        var income = new Income
        {
            Id          = "income-exact",
            Date        = new DateTime(2026, 7, 14),
            Amount      = 7777.77m,
            Source      = "Точний Клієнт",
            Description = "Дуже точний тест"
        };
        SqliteStorage.SaveProfileData(DbPath, profile.Id, SampleEntrepreneur(), [income], [], []);
        var (_, ldInc, _, _) = SqliteStorage.LoadProfileData(DbPath, profile.Id);

        Assert.Single(ldInc);
        var loaded = ldInc[0];
        Assert.Equal("income-exact",           loaded.Id);
        Assert.Equal(new DateTime(2026, 7, 14), loaded.Date.Date);
        Assert.Equal(7777.77m,                 loaded.Amount);
        Assert.Equal("Точний Клієнт",          loaded.Source);
        Assert.Equal("Дуже точний тест",        loaded.Description);
    }

    // ── SQ-09 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "SQ-09: SaveProfileData — Expense зберігається разом з CategoryName")]
    public void SaveLoad_ExpenseCategoryNamePreserved()
    {
        var profile = SqliteStorage.AddProfile(DbPath, "ExpenseCatNameTest");
        var (expenses, categories) = SampleExpensesWithCategory();

        SqliteStorage.SaveProfileData(DbPath, profile.Id, SampleEntrepreneur(), [], expenses, categories);
        var (_, _, ldExp, _) = SqliteStorage.LoadProfileData(DbPath, profile.Id);

        Assert.Single(ldExp);
        Assert.Equal("Тестова", ldExp[0].CategoryName);
        Assert.Equal("cat-test", ldExp[0].CategoryId);
    }

    // ── SQ-10 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "SQ-10: SaveProfileData — Category зберігається з Name та Description")]
    public void SaveLoad_CategoryFieldsPreserved()
    {
        var profile = SqliteStorage.AddProfile(DbPath, "CategoryFieldTest");
        var cat = new Category { Id = "cat-field", Name = "Транспорт", Description = "Авто та таксі" };

        SqliteStorage.SaveProfileData(DbPath, profile.Id, SampleEntrepreneur(), [], [], [cat]);
        var (_, _, _, ldCat) = SqliteStorage.LoadProfileData(DbPath, profile.Id);

        Assert.Single(ldCat);
        Assert.Equal("Транспорт",      ldCat[0].Name);
        Assert.Equal("Авто та таксі",  ldCat[0].Description);
    }

    // ── SQ-11 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "SQ-11: LoadProfileData — новий профіль без даних → порожні списки")]
    public void LoadProfileData_EmptyProfile_ReturnsEmptyCollections()
    {
        var profile = SqliteStorage.AddProfile(DbPath, "EmptyProfile");
        var (ldEnt, ldInc, ldExp, ldCat) = SqliteStorage.LoadProfileData(DbPath, profile.Id);

        Assert.NotNull(ldEnt);
        Assert.Empty(ldInc);
        Assert.Empty(ldExp);
        Assert.Empty(ldCat);
    }

    // ── SQ-12 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "SQ-12: SaveProfileData повторно — замінює дані (upsert)")]
    public void SaveProfileData_CalledTwice_ReplacesData()
    {
        var profile = SqliteStorage.AddProfile(DbPath, "UpsertTest");

        // Перше збереження
        var ent1 = SampleEntrepreneur("Перший ФОП");
        SqliteStorage.SaveProfileData(DbPath, profile.Id, ent1, SampleIncomes(3), [], []);

        // Друге збереження — нові дані
        var ent2 = SampleEntrepreneur("Другий ФОП");
        SqliteStorage.SaveProfileData(DbPath, profile.Id, ent2, SampleIncomes(1), [], []);

        var (ldEnt, ldInc, _, _) = SqliteStorage.LoadProfileData(DbPath, profile.Id);

        // Повинні бути дані тільки від другого збереження
        Assert.Equal("Другий ФОП", ldEnt.FullName);
        Assert.Single(ldInc);
    }

    // ── SQ-13 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "SQ-13: GetProfiles — ізоляція між профілями (різні дані)")]
    public void TwoProfiles_HaveIsolatedData()
    {
        var p1 = SqliteStorage.AddProfile(DbPath, "ПрофільА");
        var p2 = SqliteStorage.AddProfile(DbPath, "ПрофільБ");

        var ent1 = SampleEntrepreneur("ФОП А");
        var ent2 = SampleEntrepreneur("ФОП Б");

        SqliteStorage.SaveProfileData(DbPath, p1.Id, ent1, SampleIncomes(5), [], []);
        SqliteStorage.SaveProfileData(DbPath, p2.Id, ent2, SampleIncomes(2), [], []);

        var (ldEntA, ldIncA, _, _) = SqliteStorage.LoadProfileData(DbPath, p1.Id);
        var (ldEntB, ldIncB, _, _) = SqliteStorage.LoadProfileData(DbPath, p2.Id);

        Assert.Equal("ФОП А", ldEntA.FullName);
        Assert.Equal(5,       ldIncA.Count);
        Assert.Equal("ФОП Б", ldEntB.FullName);
        Assert.Equal(2,       ldIncB.Count);
    }
}
