// ════════════════════════════════════════════════════════════════════════
//  FopFinance — Модульні тести: DataStorage (допоміжні задачі)
//
//  Чек-лист №2 — Допоміжні задачі (запис/читання файлів)
//  ──────────────────────────────────────────────────────────────────────
//  DS-01  SaveToJSON/LoadFromJSON — повний round-trip даних
//  DS-02  SaveToJSON/LoadFromJSON — збереження та відновлення профілів
//  DS-03  SaveToJSON/LoadFromJSON — збереження entrepreneur
//  DS-04  SaveToJSON/LoadFromJSON — порожні колекції не породжують помилок
//  DS-05  LoadFromJSON — файл не існує → FileNotFoundException
//  DS-06  LoadFromJSON — невалідний JSON → виняток
//  DS-07  SaveToXML/LoadFromXML — повний round-trip даних
//  DS-08  SaveToXML/LoadFromXML — збереження та відновлення профілів
//  DS-09  SaveToXML/LoadFromXML — дата і суми зберігаються точно
//  DS-10  LoadFromXML — файл не існує → FileNotFoundException
//  DS-11  LoadFromXML — порожній XML → виняток
//  DS-12  SaveToJSON — файл реально створюється на диску
//  DS-13  SaveToXML — файл реально створюється на диску
//  DS-14  SaveToJSON — DecimalValue зберігається з повною точністю
//  DS-15  SaveToXML — EncodingDeclaration = utf-8 присутній
// ════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using FopFinance.Models;
using FopFinance.Storage;
using Xunit;

namespace FopFinance.Tests;

/// <summary>
/// Базовий клас-помічник: автоматично видаляє тимчасовий файл після тесту.
/// </summary>
public abstract class TempFileTestBase : IDisposable
{
    protected string TempPath { get; }

    protected TempFileTestBase(string extension)
    {
        TempPath = Path.Combine(Path.GetTempPath(), $"foptest_{Guid.NewGuid():N}.{extension}");
    }

    public void Dispose()
    {
        if (File.Exists(TempPath))
            File.Delete(TempPath);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// Тест-кейс DS-A: JSON round-trip
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Тест-кейс DS-A: Збереження та завантаження у форматі JSON.
/// Ізоляція: реальний диск (tmpPath), без зовнішніх залежностей або mocks.
/// Підхід: Arrange → Act → Assert, файл видаляється у Dispose().
/// </summary>
public class DataStorage_JsonTests : TempFileTestBase
{
    public DataStorage_JsonTests() : base("json") { }

    // ── Стандартні тестові об'єкти ───────────────────────────────────

    private static Entrepreneur SampleEntrepreneur() => new()
    {
        FullName           = "Шевченко Тарас Григорович",
        TaxGroup           = 3,
        RegistrationNumber = "3333333333"
    };

    private static List<Income> SampleIncomes() =>
    [
        new() { Id = "i-1", Date = new DateTime(2026, 1, 10), Amount = 2500.75m, Source = "ТОВ Альфа",  Description = "Консалтинг" },
        new() { Id = "i-2", Date = new DateTime(2026, 1, 25), Amount = 1000.00m, Source = "ФОП Бета",   Description = "Розробка"   }
    ];

    private static List<Expense> SampleExpenses() =>
    [
        new() { Id = "e-1", Date = new DateTime(2026, 1, 15), Amount = 500m,  CategoryId = "cat-1", CategoryName = "Оренда",  Description = "Офіс" },
        new() { Id = "e-2", Date = new DateTime(2026, 1, 20), Amount = 100.5m,CategoryId = "cat-2", CategoryName = "Реклама", Description = ""      }
    ];

    private static List<Category> SampleCategories() =>
    [
        new() { Id = "cat-1", Name = "Оренда",  Description = "Оренда офісу" },
        new() { Id = "cat-2", Name = "Реклама", Description = ""             }
    ];

    private static List<Profile> SampleProfiles() =>
    [
        new() { Id = "prof-1", Name = "Основний",  CreatedAt = new DateTime(2026, 1, 1) },
        new() { Id = "prof-2", Name = "Резервний", CreatedAt = new DateTime(2026, 1, 2) }
    ];

    // ── DS-01: Повний round-trip ──────────────────────────────────────
    [Fact(DisplayName = "DS-01: JSON round-trip — кількість доходів, витрат, категорій збережена")]
    public void JsonRoundTrip_PreservesAllCollectionCounts()
    {
        var incomes    = SampleIncomes();
        var expenses   = SampleExpenses();
        var categories = SampleCategories();
        var profiles   = SampleProfiles();

        DataStorage.SaveToJSON(TempPath, SampleEntrepreneur(), incomes, expenses, categories, profiles, "prof-1");
        var (_, ldInc, ldExp, ldCat, ldProf, activeId) = DataStorage.LoadFromJSON(TempPath);

        Assert.Equal(2, ldInc.Count);
        Assert.Equal(2, ldExp.Count);
        Assert.Equal(2, ldCat.Count);
        Assert.Equal(2, ldProf.Count);
        Assert.Equal("prof-1", activeId);
    }

    // ── DS-02: Профілі зберігаються і відновлюються ───────────────────
    [Fact(DisplayName = "DS-02: JSON round-trip — профілі зберігаються та відновлюються")]
    public void JsonRoundTrip_PreservesProfiles()
    {
        var profiles = SampleProfiles();
        DataStorage.SaveToJSON(TempPath, SampleEntrepreneur(), [], [], [], profiles, "prof-2");

        var (_, _, _, _, ldProf, activeId) = DataStorage.LoadFromJSON(TempPath);

        Assert.Equal("prof-2", activeId);
        Assert.Contains(ldProf, p => p.Id == "prof-1" && p.Name == "Основний");
        Assert.Contains(ldProf, p => p.Id == "prof-2" && p.Name == "Резервний");
    }

    // ── DS-03: Entrepreneur зберігається та відновлюється ─────────────
    [Fact(DisplayName = "DS-03: JSON round-trip — поля Entrepreneur збережені")]
    public void JsonRoundTrip_PreservesEntrepreneur()
    {
        var ent = SampleEntrepreneur();
        DataStorage.SaveToJSON(TempPath, ent, [], [], []);
        var (loaded, _, _, _, _, _) = DataStorage.LoadFromJSON(TempPath);

        Assert.Equal(ent.FullName,           loaded.FullName);
        Assert.Equal(ent.TaxGroup,           loaded.TaxGroup);
        Assert.Equal(ent.RegistrationNumber, loaded.RegistrationNumber);
    }

    // ── DS-04: Порожні колекції ───────────────────────────────────────
    [Fact(DisplayName = "DS-04: JSON round-trip — порожні колекції не породжують помилок")]
    public void JsonRoundTrip_EmptyCollections_NoException()
    {
        var ex = Record.Exception(() =>
        {
            DataStorage.SaveToJSON(TempPath, new Entrepreneur(), [], [], []);
            DataStorage.LoadFromJSON(TempPath);
        });
        Assert.Null(ex);
    }

    // ── DS-05: FileNotFoundException ──────────────────────────────────
    [Fact(DisplayName = "DS-05: LoadFromJSON — файл не існує → FileNotFoundException")]
    public void LoadFromJSON_FileNotFound_ThrowsFileNotFoundException()
    {
        var missingPath = TempPath + ".missing";
        Assert.Throws<FileNotFoundException>(() => DataStorage.LoadFromJSON(missingPath));
    }

    // ── DS-06: Невалідний JSON ───────────────────────────────────────
    [Fact(DisplayName = "DS-06: LoadFromJSON — невалідний JSON → виняток")]
    public void LoadFromJSON_InvalidJson_ThrowsException()
    {
        File.WriteAllText(TempPath, "{ this is not json !!! }");
        Assert.ThrowsAny<Exception>(() => DataStorage.LoadFromJSON(TempPath));
    }

    // ── DS-12: Файл реально створюється ──────────────────────────────
    [Fact(DisplayName = "DS-12: SaveToJSON — файл реально створюється на диску")]
    public void SaveToJSON_CreatesFileOnDisk()
    {
        DataStorage.SaveToJSON(TempPath, new Entrepreneur(), [], [], []);
        Assert.True(File.Exists(TempPath));
        Assert.True(new FileInfo(TempPath).Length > 0);
    }

    // ── DS-14: Десяткова точність ─────────────────────────────────────
    [Fact(DisplayName = "DS-14: JSON round-trip — decimal зберігається з повною точністю")]
    public void JsonRoundTrip_DecimalPrecisionPreserved()
    {
        var incomes = new List<Income>
        {
            new() { Id = "dec-1", Date = DateTime.Today, Amount = 12345.67m, Source = "Precision Test" }
        };
        DataStorage.SaveToJSON(TempPath, new Entrepreneur(), incomes, [], []);
        var (_, ldInc, _, _, _, _) = DataStorage.LoadFromJSON(TempPath);
        Assert.Equal(12345.67m, ldInc[0].Amount);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// Тест-кейс DS-B: XML round-trip
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Тест-кейс DS-B: Збереження та завантаження у форматі XML.
/// </summary>
public class DataStorage_XmlTests : TempFileTestBase
{
    public DataStorage_XmlTests() : base("xml") { }

    private static Entrepreneur SampleEntrepreneur() => new()
    {
        FullName           = "Франко Іван Якович",
        TaxGroup           = 1,
        RegistrationNumber = "1111111111"
    };

    // ── DS-07: Повний XML round-trip ─────────────────────────────────
    [Fact(DisplayName = "DS-07: XML round-trip — кількість записів збережена")]
    public void XmlRoundTrip_PreservesAllCollectionCounts()
    {
        var catId = Guid.NewGuid().ToString();
        var incomes = new List<Income>
        {
            new() { Id = "i-xml-1", Date = new DateTime(2026, 3, 1), Amount = 3000m, Source = "XML Client" }
        };
        var categories = new List<Category>
        {
            new() { Id = catId, Name = "XML Cat", Description = "Test" }
        };
        var expenses = new List<Expense>
        {
            new() { Id = "e-xml-1", Date = new DateTime(2026, 3, 5), Amount = 700m, CategoryId = catId, CategoryName = "XML Cat" }
        };
        var profiles = new List<Profile>
        {
            new() { Id = "p-xml-1", Name = "XML Profile", CreatedAt = new DateTime(2026, 3, 1) }
        };

        DataStorage.SaveToXML(TempPath, SampleEntrepreneur(), incomes, expenses, categories, profiles, "p-xml-1");
        var (_, ldInc, ldExp, ldCat, ldProf, activeId) = DataStorage.LoadFromXML(TempPath);

        Assert.Single(ldInc);
        Assert.Single(ldExp);
        Assert.Single(ldCat);
        Assert.Single(ldProf);
        Assert.Equal("p-xml-1", activeId);
    }

    // ── DS-08: Профілі в XML ─────────────────────────────────────────
    [Fact(DisplayName = "DS-08: XML round-trip — профілі зберігаються та відновлюються")]
    public void XmlRoundTrip_PreservesProfiles()
    {
        var profiles = new List<Profile>
        {
            new() { Id = "xml-a", Name = "Alpha", CreatedAt = new DateTime(2026, 4, 1) },
            new() { Id = "xml-b", Name = "Beta",  CreatedAt = new DateTime(2026, 4, 2) }
        };
        DataStorage.SaveToXML(TempPath, SampleEntrepreneur(), [], [], [], profiles, "xml-b");

        var (_, _, _, _, ldProf, activeId) = DataStorage.LoadFromXML(TempPath);

        Assert.Equal("xml-b", activeId);
        Assert.Equal(2, ldProf.Count);
        Assert.Contains(ldProf, p => p.Name == "Alpha");
        Assert.Contains(ldProf, p => p.Name == "Beta");
    }

    // ── DS-09: Дата і суми у XML ─────────────────────────────────────
    [Fact(DisplayName = "DS-09: XML round-trip — дата і суми доходу точні")]
    public void XmlRoundTrip_PreservesDateAndAmount()
    {
        var targetDate = new DateTime(2026, 6, 15);
        var incomes = new List<Income>
        {
            new() { Id = "date-test", Date = targetDate, Amount = 9876.54m, Source = "Date Test" }
        };
        DataStorage.SaveToXML(TempPath, SampleEntrepreneur(), incomes, [], []);
        var (_, ldInc, _, _, _, _) = DataStorage.LoadFromXML(TempPath);

        Assert.Equal(targetDate,  ldInc[0].Date.Date);
        Assert.Equal(9876.54m,    ldInc[0].Amount);
        Assert.Equal("Date Test", ldInc[0].Source);
    }

    // ── DS-10: FileNotFoundException (XML) ────────────────────────────
    [Fact(DisplayName = "DS-10: LoadFromXML — файл не існує → FileNotFoundException")]
    public void LoadFromXML_FileNotFound_ThrowsFileNotFoundException()
    {
        var missingPath = TempPath + ".missing";
        Assert.Throws<FileNotFoundException>(() => DataStorage.LoadFromXML(missingPath));
    }

    // ── DS-11: Порожній XML ──────────────────────────────────────────
    [Fact(DisplayName = "DS-11: LoadFromXML — порожній XML-файл → виняток")]
    public void LoadFromXML_EmptyFile_ThrowsException()
    {
        File.WriteAllText(TempPath, "");
        Assert.ThrowsAny<Exception>(() => DataStorage.LoadFromXML(TempPath));
    }

    // ── DS-13: Файл реально створюється ──────────────────────────────
    [Fact(DisplayName = "DS-13: SaveToXML — файл реально створюється на диску")]
    public void SaveToXML_CreatesFileOnDisk()
    {
        DataStorage.SaveToXML(TempPath, SampleEntrepreneur(), [], [], []);
        Assert.True(File.Exists(TempPath));
        Assert.True(new FileInfo(TempPath).Length > 0);
    }

    // ── DS-15: UTF-8 декларація ───────────────────────────────────────
    [Fact(DisplayName = "DS-15: SaveToXML — XML-файл містить utf-8 encoding declaration")]
    public void SaveToXML_ContainsUtf8Declaration()
    {
        DataStorage.SaveToXML(TempPath, SampleEntrepreneur(), [], [], []);
        var content = File.ReadAllText(TempPath);
        Assert.Contains("utf-8", content, StringComparison.OrdinalIgnoreCase);
    }

    // ── DS-bonus: EntrepreneurFields у XML ────────────────────────────
    [Fact(DisplayName = "DS-bonus: XML round-trip — поля Entrepreneur збережені")]
    public void XmlRoundTrip_PreservesEntrepreneur()
    {
        var ent = SampleEntrepreneur();
        DataStorage.SaveToXML(TempPath, ent, [], [], []);
        var (loaded, _, _, _, _, _) = DataStorage.LoadFromXML(TempPath);

        Assert.Equal(ent.FullName,           loaded.FullName);
        Assert.Equal(ent.TaxGroup,           loaded.TaxGroup);
        Assert.Equal(ent.RegistrationNumber, loaded.RegistrationNumber);
    }
}
