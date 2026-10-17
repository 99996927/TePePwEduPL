using System.Security.Cryptography;
using System.Text;

namespace Hr.Core.Seeding.Demo;

/// <summary>Deterministyczne identyfikatory danych demo: ten sam (ziarno, klucz) daje ten sam Guid.</summary>
public static class DemoIds
{
    public static Guid For(int seed, string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{key}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>Stabilne ziarno <see cref="Random"/> dla osoby: zależy od (ziarno, login), nie od uruchomienia procesu.</summary>
    public static int SeedFor(int seed, string login) =>
        BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:seed:{login}")), 0);
}
