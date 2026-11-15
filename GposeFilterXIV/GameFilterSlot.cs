using System;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;

namespace GposeFilterXIV;

/// <summary>
/// Le vrai emplacement du filtre, dans EnvManager. À utiliser sur le fil du jeu (Framework.Update) seulement.
/// </summary>
internal sealed unsafe class GameFilterSlot : IFilterSlot
{
    private readonly GameAddresses addresses;
    private readonly delegate* unmanaged<nint, int, byte, byte, void> reserve;
    private EnvManager* env;

    public GameFilterSlot(GameAddresses addresses)
    {
        this.addresses = addresses;
        reserve = (delegate* unmanaged<nint, int, byte, byte, void>)addresses.ReserveFunction;
    }

    /// <summary>La position de ColorFilterOverride dans EnvManager selon ClientStructs.</summary>
    public static int OverrideOffset
    {
        get
        {
            // Sur une copie locale : .NET vérifie le pointeur nul même pour prendre l'adresse d'un champ.
            var env = default(EnvManager);
            return (int)((byte*)&env.ColorFilterOverride - (byte*)&env);
        }
    }

    /// <summary>La taille d'EnvManager selon ClientStructs.</summary>
    public static int EnvManagerSize => sizeof(EnvManager);

    /// <summary>
    /// Cherche les adresses dans le code du jeu. La signature doit être trouvée une seule fois ; le code est lu dans la
    /// copie du module que garde Dalamud (les crochets d'autres plugins n'y sont pas), sans sortir de la section de code.
    /// </summary>
    public static GameAddresses? Locate(ISigScanner scanner, out string? error)
    {
        nint[] sites;
        try
        {
            sites = scanner.ScanAllText(GameCode.SetColorFilterSignature);
        }
        catch (Exception e)
        {
            error = $"Signature scan failed: {e.Message}";
            return null;
        }

        if (sites.Length != 1)
        {
            error = $"SetColorFilter signature found {sites.Length} times (expected once).";
            return null;
        }

        var readCode = GameCode.CodeReader(
            scanner.Module.BaseAddress,
            scanner.TextSectionOffset,
            scanner.TextSectionSize,
            scanner.SearchBase,
            (address, length) => new ReadOnlySpan<byte>((void*)address, length).ToArray());

        return GameCode.Resolve(sites.Single(), readCode, OverrideOffset, EnvManagerSize, out error);
    }

    /// <summary>
    /// L'emplacement pour cette image, ou null sans EnvManager (écran de chargement). EnvManager est relu à chaque
    /// image : on ne garde jamais son adresse d'une image à l'autre.
    /// </summary>
    public IFilterSlot? Current()
    {
        env = EnvManager.Instance();
        return env == null ? null : this;
    }

    public byte Owner => *((byte*)env + addresses.OwnerOffset);

    public void Reserve(bool enable) =>
        reserve((nint)env + addresses.ReserveArgumentOffset, GameCode.Slot, enable ? (byte)1 : (byte)0, FilterSlot.OurOwner);

    public void Write(in EnvColorFilterParameters values) => env->ColorFilterOverride = values;
}
