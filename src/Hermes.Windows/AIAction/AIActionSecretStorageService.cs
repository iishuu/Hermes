using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.AIAction;

public sealed class AIActionSecretStorageService
{
    private readonly AppLogger _logger;

    public AIActionSecretStorageService(AppLogger logger)
    {
        _logger = logger;
    }

    public async Task SaveApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        AIActionPaths.EnsureCreated();
        var protectedBytes = Protect(Encoding.UTF8.GetBytes(apiKey));
        await File.WriteAllBytesAsync(AIActionPaths.SecretPath, protectedBytes, cancellationToken);
    }

    public async Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(AIActionPaths.SecretPath))
        {
            return null;
        }

        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(AIActionPaths.SecretPath, cancellationToken);
            return Encoding.UTF8.GetString(Unprotect(protectedBytes));
        }
        catch (Exception ex)
        {
            _logger.Warning($"AI Action API key could not be decrypted. {ex.Message}");
            return null;
        }
    }

    public bool HasApiKey() => File.Exists(AIActionPaths.SecretPath);

    private static byte[] Protect(byte[] data)
    {
        var input = CreateBlob(data);
        try
        {
            if (!NativeMethods.CryptProtectData(ref input, "Hermes AI Action API key", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output))
            {
                throw new InvalidOperationException($"CryptProtectData failed: {Marshal.GetLastWin32Error()}");
            }

            try
            {
                return ReadBlob(output);
            }
            finally
            {
                NativeMethods.LocalFree(output.pbData);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(input.pbData);
        }
    }

    private static byte[] Unprotect(byte[] protectedData)
    {
        var input = CreateBlob(protectedData);
        try
        {
            if (!NativeMethods.CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output))
            {
                throw new InvalidOperationException($"CryptUnprotectData failed: {Marshal.GetLastWin32Error()}");
            }

            try
            {
                return ReadBlob(output);
            }
            finally
            {
                NativeMethods.LocalFree(output.pbData);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(input.pbData);
        }
    }

    private static NativeMethods.DATA_BLOB CreateBlob(byte[] data)
    {
        var blob = new NativeMethods.DATA_BLOB
        {
            cbData = data.Length,
            pbData = Marshal.AllocHGlobal(data.Length)
        };
        Marshal.Copy(data, 0, blob.pbData, data.Length);
        return blob;
    }

    private static byte[] ReadBlob(NativeMethods.DATA_BLOB blob)
    {
        var data = new byte[blob.cbData];
        Marshal.Copy(blob.pbData, data, 0, blob.cbData);
        return data;
    }
}
