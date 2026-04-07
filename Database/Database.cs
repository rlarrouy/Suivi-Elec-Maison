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
        public static async Task<DataTable> GetMeasuresAsync(int limit = 1000)
        {
            var dt = new DataTable();
            using var conn = await GetOpenConnectionAsync();
            // Utiliser un nom de table entre guillemets pour respecter la casse éventuelle
            //var sql = $"SELECT * FROM public.\"Mesures\" ORDER BY id DESC LIMIT {limit}";
            var sql = $"SELECT \"Id_Mesure\", \"Jour\", \"Production\", \"Stockage\", \"Autoconsommation\", \"Conso_Batterie\", \"Conso_Reseau\", \"Conso_Totale\" FROM public.\"Mesures\" ORDER BY id DESC LIMIT {limit}";
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt;
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
