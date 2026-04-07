ConfigEncryptor

Small command-line tool to create an encrypted `connection.enc` file for the WPF app.

Build:
  dotnet build Tools/ConfigEncryptor/ConfigEncryptor.csproj

Usage:
  dotnet run --project Tools/ConfigEncryptor/ConfigEncryptor.csproj -- "\"Host=...;Port=...;Username=...;Password=...;Database=...;Ssl Mode=Require;Trust Server Certificate=true\"" "D:\Path\to\connection.enc"

If the output path is omitted, `connection.enc` will be created in the current directory.

Notes:
- The tool uses DPAPI (ProtectedData) with DataProtectionScope.CurrentUser, matching the WPF application's decryption.
- Create the `connection.enc` file using the same Windows user that will run the WPF app.
