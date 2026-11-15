using System.Buffers.Binary;
using Lumina;

namespace GposeFilterXIV.Tests;

/// <summary>
/// Lecture seule des fichiers du jeu installé. Sans le jeu, les tests qui en ont besoin échouent, sauf avec la variable
/// d'environnement GPOSEFILTER_TESTS_WITHOUT_GAME=1 (machine sans le jeu, comme GitHub Actions) : ils ne vérifient alors rien.
/// </summary>
internal static class GameFiles
{
    private const string GamePath = @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game";

    private static readonly Lazy<bool> Present = new(() =>
    {
        if (Directory.Exists(GamePath))
            return true;
        if (Environment.GetEnvironmentVariable("GPOSEFILTER_TESTS_WITHOUT_GAME") == "1")
            return false;
        throw new InvalidOperationException($"Game not found in {GamePath} (set GPOSEFILTER_TESTS_WITHOUT_GAME=1 to skip).");
    });

    private static readonly Lazy<GameData?> DataValue = new(() =>
        Present.Value ? new GameData(Path.Combine(GamePath, "sqpack"), new LuminaOptions { PanicOnSheetChecksumMismatch = false }) : null);

    private static readonly Lazy<GameExe?> ExeValue = new(() =>
        Present.Value ? new GameExe(File.ReadAllBytes(Path.Combine(GamePath, "ffxiv_dx11.exe"))) : null);

    /// <summary>Les fichiers de données du jeu, ou null si on a demandé de se passer du jeu.</summary>
    public static GameData? Data => DataValue.Value;

    /// <summary>ffxiv_dx11.exe, ou null si on a demandé de se passer du jeu.</summary>
    public static GameExe? Exe => ExeValue.Value;

    /// <summary>La version du jeu installé (game\ffxivgame.ver).</summary>
    public static string Version => File.ReadAllText(Path.Combine(GamePath, "ffxivgame.ver")).Trim();
}

/// <summary>L'exécutable du jeu lu comme un fichier : sections, adresses comme une fois chargé à son adresse de base.</summary>
internal sealed class GameExe
{
    private readonly byte[] file;
    private readonly long imageBase;
    private readonly (uint Va, uint Size, uint Raw)[] text;

    public GameExe(byte[] file)
    {
        this.file = file;
        int pe = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(0x3C));
        int sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(pe + 6));
        int optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(pe + 20));
        int optional = pe + 24;
        imageBase = BinaryPrimitives.ReadInt64LittleEndian(file.AsSpan(optional + 24));

        var sections = new List<(uint, uint, uint)>();
        for (int i = 0, s = optional + optionalSize; i < sectionCount; i++, s += 40)
        {
            const uint Executable = 0x20000000;
            uint characteristics = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(s + 36));
            if ((characteristics & Executable) == 0)
                continue;
            uint va = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(s + 12));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(s + 16));
            uint raw = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(s + 20));
            sections.Add((va, size, raw));
        }

        text = sections.ToArray();
    }

    /// <summary>Toutes les adresses où le motif se trouve dans le code.</summary>
    public List<nint> FindAll(string pattern)
    {
        var parts = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bytes = parts.Select(p => p == "??" ? -1 : Convert.ToInt32(p, 16)).ToArray();
        var found = new List<nint>();
        foreach (var (va, size, raw) in text)
        {
            var section = file.AsSpan((int)raw, (int)size);
            for (int i = section.IndexOf((byte)bytes[0]); i >= 0 && i + bytes.Length <= section.Length;)
            {
                bool match = true;
                for (int j = 1; j < bytes.Length && match; j++)
                    match = bytes[j] < 0 || section[i + j] == bytes[j];
                if (match)
                    found.Add((nint)(imageBase + va + i));

                int next = section[(i + 1)..].IndexOf((byte)bytes[0]);
                i = next < 0 ? -1 : i + 1 + next;
            }
        }

        return found;
    }

    /// <summary>Lit le code à une adresse, ou null hors des sections de code.</summary>
    public byte[]? Read(nint address, int length)
    {
        foreach (var (va, size, raw) in text)
        {
            long start = imageBase + va;
            if (address >= start && address + length <= start + size)
                return file.AsSpan((int)(raw + (address - start)), length).ToArray();
        }

        return null;
    }
}
