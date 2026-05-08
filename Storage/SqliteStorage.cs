using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using FopFinance.Models;

namespace FopFinance.Storage
{
    public static class SqliteStorage
    {
        private static SqliteConnection OpenConnection(string dbPath)
        {
            var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();

            using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();

            return conn;
        }

        public static void EnsureDatabase(string dbPath)
        {
            using var conn = OpenConnection(dbPath);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS profiles (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS entrepreneurs (
  profile_id TEXT PRIMARY KEY,
  id TEXT NOT NULL,
  full_name TEXT NOT NULL,
  tax_group INTEGER NOT NULL,
  registration_number TEXT NOT NULL,
  FOREIGN KEY(profile_id) REFERENCES profiles(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS categories (
    id TEXT NOT NULL,
    profile_id TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    PRIMARY KEY (id, profile_id),
    FOREIGN KEY(profile_id) REFERENCES profiles(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS incomes (
    id TEXT NOT NULL,
    profile_id TEXT NOT NULL,
    date TEXT NOT NULL,
    amount TEXT NOT NULL,
    source TEXT NOT NULL,
    description TEXT NOT NULL,
    PRIMARY KEY (id, profile_id),
    FOREIGN KEY(profile_id) REFERENCES profiles(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS expenses (
    id TEXT NOT NULL,
    profile_id TEXT NOT NULL,
    date TEXT NOT NULL,
    amount TEXT NOT NULL,
    category_id TEXT NOT NULL,
    category_name TEXT NOT NULL,
    description TEXT NOT NULL,
    PRIMARY KEY (id, profile_id),
    FOREIGN KEY(profile_id) REFERENCES profiles(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_incomes_profile_date ON incomes(profile_id, date);
CREATE INDEX IF NOT EXISTS ix_expenses_profile_date ON expenses(profile_id, date);
CREATE INDEX IF NOT EXISTS ix_expenses_profile_category ON expenses(profile_id, category_id);
";
            cmd.ExecuteNonQuery();
        }

        public static List<Profile> GetProfiles(string dbPath)
        {
            var list = new List<Profile>();
            using var conn = OpenConnection(dbPath);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, created_at FROM profiles ORDER BY datetime(created_at), name";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Profile
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    CreatedAt = DateTime.TryParse(reader.GetString(2), out var dt) ? dt : DateTime.UtcNow
                });
            }

            return list;
        }

        public static Profile AddProfile(string dbPath, string name)
        {
            var profile = new Profile { Name = name.Trim() };

            using var conn = OpenConnection(dbPath);

            using var tx = conn.BeginTransaction();

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO profiles(id, name, created_at) VALUES(@id, @name, @createdAt)";
                cmd.Parameters.AddWithValue("@id", profile.Id);
                cmd.Parameters.AddWithValue("@name", profile.Name);
                cmd.Parameters.AddWithValue("@createdAt", profile.CreatedAt.ToString("o"));
                cmd.ExecuteNonQuery();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO entrepreneurs(profile_id, id, full_name, tax_group, registration_number)
VALUES(@profileId, @id, @fullName, @taxGroup, @regNumber)";
                cmd.Parameters.AddWithValue("@profileId", profile.Id);
                cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("@fullName", profile.Name);
                cmd.Parameters.AddWithValue("@taxGroup", 3);
                cmd.Parameters.AddWithValue("@regNumber", string.Empty);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return profile;
        }

        public static void UpsertProfiles(string dbPath, IEnumerable<Profile> profiles)
        {
            using var conn = OpenConnection(dbPath);
            using var tx = conn.BeginTransaction();

            foreach (var profile in profiles ?? Array.Empty<Profile>())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO profiles(id, name, created_at) VALUES(@id, @name, @createdAt)
ON CONFLICT(id) DO UPDATE SET
name=excluded.name,
created_at=excluded.created_at";
                    cmd.Parameters.AddWithValue("@id", profile.Id);
                    cmd.Parameters.AddWithValue("@name", profile.Name);
                    cmd.Parameters.AddWithValue("@createdAt", profile.CreatedAt.ToString("o"));
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO entrepreneurs(profile_id, id, full_name, tax_group, registration_number)
VALUES(@profileId, @id, @fullName, @taxGroup, @regNumber)
ON CONFLICT(profile_id) DO NOTHING";
                    cmd.Parameters.AddWithValue("@profileId", profile.Id);
                    cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                    cmd.Parameters.AddWithValue("@fullName", profile.Name ?? string.Empty);
                    cmd.Parameters.AddWithValue("@taxGroup", 3);
                    cmd.Parameters.AddWithValue("@regNumber", string.Empty);
                    cmd.ExecuteNonQuery();
                }
            }

            tx.Commit();
        }

        public static void SaveProfileData(string dbPath, string profileId, Entrepreneur entrepreneur, List<Income> incomes, List<Expense> expenses, List<Category> categories)
        {
            using var conn = OpenConnection(dbPath);
            using var tx = conn.BeginTransaction();

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO entrepreneurs(profile_id, id, full_name, tax_group, registration_number)
VALUES(@profileId, @id, @fullName, @taxGroup, @regNumber)
ON CONFLICT(profile_id) DO UPDATE SET
id=excluded.id,
full_name=excluded.full_name,
tax_group=excluded.tax_group,
registration_number=excluded.registration_number";
                cmd.Parameters.AddWithValue("@profileId", profileId);
                cmd.Parameters.AddWithValue("@id", entrepreneur.Id);
                cmd.Parameters.AddWithValue("@fullName", entrepreneur.FullName ?? string.Empty);
                cmd.Parameters.AddWithValue("@taxGroup", entrepreneur.TaxGroup);
                cmd.Parameters.AddWithValue("@regNumber", entrepreneur.RegistrationNumber ?? string.Empty);
                cmd.ExecuteNonQuery();
            }

            DeleteByProfile(conn, tx, "incomes", profileId);
            DeleteByProfile(conn, tx, "expenses", profileId);
            DeleteByProfile(conn, tx, "categories", profileId);

            foreach (var c in categories)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO categories(id, profile_id, name, description) VALUES(@id, @profileId, @name, @desc)";
                cmd.Parameters.AddWithValue("@id", c.Id);
                cmd.Parameters.AddWithValue("@profileId", profileId);
                cmd.Parameters.AddWithValue("@name", c.Name ?? string.Empty);
                cmd.Parameters.AddWithValue("@desc", c.Description ?? string.Empty);
                cmd.ExecuteNonQuery();
            }

            foreach (var i in incomes)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO incomes(id, profile_id, date, amount, source, description)
VALUES(@id, @profileId, @date, @amount, @source, @desc)";
                cmd.Parameters.AddWithValue("@id", i.Id);
                cmd.Parameters.AddWithValue("@profileId", profileId);
                cmd.Parameters.AddWithValue("@date", i.Date.ToString("o"));
                cmd.Parameters.AddWithValue("@amount", i.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@source", i.Source ?? string.Empty);
                cmd.Parameters.AddWithValue("@desc", i.Description ?? string.Empty);
                cmd.ExecuteNonQuery();
            }

            foreach (var e in expenses)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO expenses(id, profile_id, date, amount, category_id, category_name, description)
VALUES(@id, @profileId, @date, @amount, @categoryId, @categoryName, @desc)";
                cmd.Parameters.AddWithValue("@id", e.Id);
                cmd.Parameters.AddWithValue("@profileId", profileId);
                cmd.Parameters.AddWithValue("@date", e.Date.ToString("o"));
                cmd.Parameters.AddWithValue("@amount", e.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@categoryId", e.CategoryId ?? string.Empty);
                cmd.Parameters.AddWithValue("@categoryName", e.CategoryName ?? string.Empty);
                cmd.Parameters.AddWithValue("@desc", e.Description ?? string.Empty);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        public static (Entrepreneur entrepreneur, List<Income> incomes, List<Expense> expenses, List<Category> categories)
            LoadProfileData(string dbPath, string profileId)
        {
            var entrepreneur = new Entrepreneur();
            var incomes = new List<Income>();
            var expenses = new List<Expense>();
            var categories = new List<Category>();

            using var conn = OpenConnection(dbPath);

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, full_name, tax_group, registration_number FROM entrepreneurs WHERE profile_id=@profileId";
                cmd.Parameters.AddWithValue("@profileId", profileId);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    entrepreneur = new Entrepreneur
                    {
                        Id = reader.GetString(0),
                        FullName = reader.GetString(1),
                        TaxGroup = reader.GetInt32(2),
                        RegistrationNumber = reader.GetString(3)
                    };
                }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, name, description FROM categories WHERE profile_id=@profileId ORDER BY name";
                cmd.Parameters.AddWithValue("@profileId", profileId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    categories.Add(new Category
                    {
                        Id = reader.GetString(0),
                        Name = reader.GetString(1),
                        Description = reader.GetString(2)
                    });
                }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, date, amount, source, description FROM incomes WHERE profile_id=@profileId";
                cmd.Parameters.AddWithValue("@profileId", profileId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    incomes.Add(new Income
                    {
                        Id = reader.GetString(0),
                        Date = DateTime.TryParse(reader.GetString(1), out var d) ? d : DateTime.Today,
                        Amount = decimal.TryParse(reader.GetString(2), NumberStyles.Number, CultureInfo.InvariantCulture, out var a) ? a : 0m,
                        Source = reader.GetString(3),
                        Description = reader.GetString(4)
                    });
                }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, date, amount, category_id, category_name, description FROM expenses WHERE profile_id=@profileId";
                cmd.Parameters.AddWithValue("@profileId", profileId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    expenses.Add(new Expense
                    {
                        Id = reader.GetString(0),
                        Date = DateTime.TryParse(reader.GetString(1), out var d) ? d : DateTime.Today,
                        Amount = decimal.TryParse(reader.GetString(2), NumberStyles.Number, CultureInfo.InvariantCulture, out var a) ? a : 0m,
                        CategoryId = reader.GetString(3),
                        CategoryName = reader.GetString(4),
                        Description = reader.GetString(5)
                    });
                }
            }

            return (entrepreneur, incomes, expenses, categories);
        }

        private static void DeleteByProfile(SqliteConnection conn, SqliteTransaction tx, string tableName, string profileId)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"DELETE FROM {tableName} WHERE profile_id=@profileId";
            cmd.Parameters.AddWithValue("@profileId", profileId);
            cmd.ExecuteNonQuery();
        }
    }
}
