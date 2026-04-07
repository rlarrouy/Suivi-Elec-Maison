using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Security.Cryptography;
using Npgsql;

namespace Suivi_Elec_Maison.Database
{
    public static class DatabaseHelper
    {
        // Chemins par défaut des fichiers de configuration (dans le répertoire de l'application)
        private static readonly string DefaultEncryptedConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connection.enc");
        private static readonly string DefaultPlainConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connection.cfg");

        private static string GetConnectionString()
        {
            // Priorité : fichier en clair (connection.cfg) s'il existe, sinon fichier chiffré (connection.enc)
            if (File.Exists(DefaultPlainConfigPath))
            {
                return EncryptedConfig.ReadPlainConnectionString(DefaultPlainConfigPath);
            }

            if (File.Exists(DefaultEncryptedConfigPath))
            {
                return EncryptedConfig.ReadConnectionString(DefaultEncryptedConfigPath);
            }

            throw new FileNotFoundException($"Aucun fichier de configuration trouvé. Cherché : {DefaultPlainConfigPath} et {DefaultEncryptedConfigPath}");
        }

        public static async Task<NpgsqlConnection> GetOpenConnectionAsync()
        {
            var cs = GetConnectionString();
            var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            return conn;
        }

        // Récupère les dernières mesures depuis la table "Mesures" et les retourne sous forme de DataTable.
        // Méthode robuste : détecte les colonnes réelles et choisit une colonne d'ORDER BY adaptée
        // pour éviter l'erreur 'column "id" does not exist'.
        public static async Task<DataTable> GetMeasuresAsync(int limit = 1000)
        {
            var dt = new DataTable();
            using var conn = await GetOpenConnectionAsync();

            // Récupérer le nom réel de la table et la liste des colonnes depuis information_schema
            var columns = new System.Collections.Generic.List<string>();
            string actualTableName = null;
            var sqlCols = @"SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = 'public' AND (table_name = @t OR table_name = lower(@t))
ORDER BY ordinal_position";

            using (var cmd = new NpgsqlCommand(sqlCols, conn))
            {
                cmd.Parameters.AddWithValue("t", "Mesures");
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (actualTableName == null) actualTableName = reader.GetString(0);
                    columns.Add(reader.GetString(1));
                }
            }

            // Si la table n'a pas été trouvée, tenter une sélection simple (non ordonnée)
            if (columns.Count == 0)
            {
                try
                {
                    using var cmd = new NpgsqlCommand("SELECT * FROM Mesures LIMIT @limit", conn);
                    cmd.Parameters.AddWithValue("limit", limit);
                    using var reader = await cmd.ExecuteReaderAsync();
                    dt.Load(reader);
                    return dt;
                }
                catch (PostgresException)
                {
                    throw new Exception("La table 'Mesures' est introuvable dans le schéma public.");
                }
            }

            // Déterminer si le nom de table doit être cité (s'il contient des majuscules ou caractères spéciaux)
            bool needsQuoting = actualTableName != actualTableName.ToLowerInvariant();
            string tableRef = needsQuoting ? $"public.\"{actualTableName.Replace("\"", "\"\"") }\"" : $"public.{actualTableName}";

            // Choix d'une colonne pour ORDER BY : préférer id, *_id, timestamp/date
            string orderCol = null;
            string[] preferred = new[] { "id", "measurement_id", "mesure_id", "timestamp", "time", "date", "created_at", "created" };
            foreach (var p in preferred)
            {
                var found = columns.Find(c => string.Equals(c, p, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    orderCol = found;
                    break;
                }
            }
            if (orderCol == null)
            {
                var found = columns.Find(c => c.EndsWith("_id", StringComparison.OrdinalIgnoreCase));
                if (found != null) orderCol = found;
            }

            string sql;
            if (!string.IsNullOrEmpty(orderCol))
            {
                bool colNeedsQuote = orderCol != orderCol.ToLowerInvariant();
                var colRef = colNeedsQuote ? $"\"{orderCol.Replace("\"", "\"\"") }\"" : orderCol;
                sql = $"SELECT * FROM {tableRef} ORDER BY {colRef} DESC LIMIT @limit";
            }
            else
            {
                sql = $"SELECT * FROM {tableRef} LIMIT @limit";
            }

            try
            {
                using var cmd2 = new NpgsqlCommand(sql, conn);
                cmd2.Parameters.AddWithValue("limit", limit);
                using var reader2 = await cmd2.ExecuteReaderAsync();
                dt.Load(reader2);
                return dt;
            }
            catch (PostgresException ex)
            {
                // Si la colonne d'ordre n'existe pas, retenter sans ORDER BY
                if (ex.SqlState == "42703")
                {
                    using var cmd3 = new NpgsqlCommand($"SELECT * FROM {tableRef} LIMIT @limit", conn);
                    cmd3.Parameters.AddWithValue("limit", limit);
                    using var reader3 = await cmd3.ExecuteReaderAsync();
                    dt.Load(reader3);
                    return dt;
                }

                throw;
            }
        }
    }

    public static class EncryptedConfig
    {
        // Entropy optionnelle pour renforcer la protection ; facultative mais ajoutée ici
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Suivi_Elec_Maison_Entropy_v1");

        public static string ReadConnectionString(string encryptedFilePath)
        {
            if (!File.Exists(encryptedFilePath))
                throw new FileNotFoundException($"Fichier de configuration chiffré introuvable : {encryptedFilePath}");

            var encrypted = File.ReadAllBytes(encryptedFilePath);
            var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }

        // Méthode utilitaire pour créer le fichier chiffré à partir d'une chaîne de connexion en clair.
        // Utiliser uniquement en développement pour générer `connection.enc` sur la machine de déploiement.
        public static void CreateEncryptedFile(string encryptedFilePath, string connectionString)
        {
            var plain = Encoding.UTF8.GetBytes(connectionString);
            var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(encryptedFilePath, encrypted);
        }

        // Lire un fichier de configuration en clair (connection.cfg)
        public static string ReadPlainConnectionString(string plainFilePath)
        {
            if (!File.Exists(plainFilePath))
                throw new FileNotFoundException($"Fichier de configuration introuvable : {plainFilePath}");

            return File.ReadAllText(plainFilePath, Encoding.UTF8).Trim();
        }

        // Écrire un fichier de configuration en clair (connection.cfg)
        public static void CreatePlainFile(string plainFilePath, string connectionString)
        {
            File.WriteAllText(plainFilePath, connectionString, Encoding.UTF8);
        }
    }
}
