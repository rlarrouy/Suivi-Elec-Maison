using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace ConfigEncryptor
{
    internal static class Program
    {
        // Même entropy que dans Database.EncryptedConfig
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Suivi_Elec_Maison_Entropy_v1");

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: ConfigEncryptor <connectionString> [outputPath]");
            Console.WriteLine("Si outputPath n'est pas fourni, 'connection.enc' sera créé dans le répertoire courant.");
        }

        private static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return 1;
            }

            var cs = args[0];
            var outPath = args.Length >= 2 ? args[1] : Path.Combine(Environment.CurrentDirectory, "connection.enc");

            try
            {
                var plain = Encoding.UTF8.GetBytes(cs);
                var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(outPath, encrypted);
                Console.WriteLine($"Fichier chiffré créé : {outPath}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Erreur lors du chiffrement : {ex.Message}");
                return 2;
            }
        }
    }
}
