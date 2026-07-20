using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;

namespace Kalm.Api.Configuration;

public static class DataProtectionConfiguration
{
    public static void AddKalmDataProtection(this WebApplicationBuilder builder)
    {
        bool productionLike = builder.Environment.IsProduction() || builder.Environment.IsStaging();
        string? configuredPath = builder.Configuration["DataProtection:KeyRingPath"];
        string path = configuredPath ?? (builder.Environment.IsDevelopment()
            ? Path.Combine(Path.GetTempPath(), "Kalm", "DataProtection-Keys")
            : string.Empty);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("A persistent Data Protection key-ring path is required.");
        }

        Directory.CreateDirectory(path);
        VerifyReadWriteAccess(path);
        IDataProtectionBuilder protection = builder.Services.AddDataProtection()
            .SetApplicationName("Kalm.Management")
            .PersistKeysToFileSystem(new DirectoryInfo(path));

        string? certificatePath = builder.Configuration["DataProtection:CertificatePath"];
        if (productionLike && string.IsNullOrWhiteSpace(certificatePath))
        {
            throw new InvalidOperationException("Data Protection keys must be protected at rest in staging and production.");
        }

        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            string password = builder.Configuration["DataProtection:CertificatePassword"] ?? string.Empty;
            X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, password);
            if (!certificate.HasPrivateKey)
            {
                certificate.Dispose();
                throw new InvalidOperationException("The Data Protection certificate must contain a private key.");
            }

            protection.ProtectKeysWithCertificate(certificate);
        }
    }

    private static void VerifyReadWriteAccess(string path)
    {
        string probe = Path.Combine(path, $".kalm-write-probe-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllBytes(probe, []);
            using FileStream stream = File.OpenRead(probe);
            _ = stream.Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("The Data Protection key-ring path must be readable and writable.", exception);
        }
        finally
        {
            File.Delete(probe);
        }
    }
}
