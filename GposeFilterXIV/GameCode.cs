using System;
using System.Buffers.Binary;
using System.Linq;

namespace GposeFilterXIV;

/// <summary>Ce que le plugin a besoin de connaître dans le jeu, relu dans son code au démarrage.</summary>
/// <param name="ReserveFunction">La fonction qui réserve ou libère un emplacement d'effet (0x140066E50 en 2026.11.03).</param>
/// <param name="ReserveArgumentOffset">Son premier argument : EnvManager + cet écart (0x358).</param>
/// <param name="OwnerOffset">L'octet propriétaire de l'emplacement du filtre : EnvManager + cet écart (0x45B).</param>
internal sealed record GameAddresses(nint ReserveFunction, int ReserveArgumentOffset, int OwnerOffset);

/// <summary>
/// Lecture du code du jeu : rien n'est écrit tant que le code ne ressemble pas exactement à celui qu'on a lu
/// (version 2026.11.03). Une mise à jour qui le change met le plugin en pause au lieu d'écrire au mauvais endroit.
/// </summary>
internal static class GameCode
{
    /// <summary>L'emplacement d'effet du filtre de couleur (n° de bit de PostEffectFlags : ColorFilterDarkBlend).</summary>
    public const int Slot = 15;

    /// <summary>
    /// Fin de SetColorFilter (0x1417DC190), qui réserve l'emplacement pour GPose :
    /// <code>
    /// mov r9b, 0x40                    ; propriétaire GPose / scènes
    /// mov rbx, [EnvManager.Instance]
    /// mov r8b, 1                       ; réserver
    /// mov edx, 0xF                     ; emplacement 15
    /// lea rcx, [rbx+0x358]             ; premier argument
    /// call 0x140066E50                 ; réservation
    /// xor ecx, ecx
    /// lea rax, [rbx+0x3D0]             ; ColorFilterOverride
    /// cmp byte ptr [rbx+0x45B], 0x40   ; octet propriétaire
    /// </code>
    /// </summary>
    public const string SetColorFilterSignature =
        "41 B1 40 48 8B 1D ?? ?? ?? ?? 41 B0 01 BA 0F 00 00 00 48 8D 8B ?? ?? ?? ?? E8 ?? ?? ?? ?? "
        + "33 C9 48 8D 83 ?? ?? ?? ?? 80 BB ?? ?? ?? ?? 40";

    /// <summary>
    /// La fonction de réservation en entier (rcx = EnvManager + 0x358, edx = emplacement, r8b = réserver, r9b = propriétaire) :
    /// <code>
    /// movsxd rax, edx
    /// movzx r10d, byte ptr [rax+rcx+0xF4]   ; propriétaire actuel
    /// test r8b, r8b / je libérer
    /// cmp r10b, r9b / jae fin               ; réserver : le plus grand propriétaire gagne
    /// mov [rax+rcx+0xF4], r9b / ret
    /// libérer: cmp r10b, r9b / jne fin      ; libérer : seulement si c'est le même propriétaire
    /// mov byte ptr [rax+rcx+0xF4], 0
    /// xor r8d, r8d / mov rcx, [PostEffectManager.Instance] / jmp SetEffectEnabled
    /// fin: ret
    /// </code>
    /// </summary>
    public const string ReserveSignature =
        "48 63 C2 44 0F B6 94 08 ?? ?? ?? ?? 45 84 C0 74 0E 45 3A D1 73 25 44 88 8C 08 ?? ?? ?? ?? C3 "
        + "45 3A D1 75 17 C6 84 08 ?? ?? ?? ?? 00 45 33 C0 48 8B 0D ?? ?? ?? ?? E9 ?? ?? ?? ?? C3";

    public static readonly int SetColorFilterLength = PatternLength(SetColorFilterSignature);
    public static readonly int ReserveLength = PatternLength(ReserveSignature);

    // Positions dans SetColorFilterSignature.
    private const int ArgumentDisp = 21;
    private const int CallRel = 26;
    private const int CallEnd = 30;
    private const int OverrideDisp = 35;
    private const int OwnerDisp = 41;

    // Positions des trois accès au tableau des propriétaires dans ReserveSignature.
    private static readonly int[] OwnersArrayDisps = [8, 26, 39];

    /// <summary>
    /// Relit les adresses dans le code du jeu.
    /// </summary>
    /// <param name="siteAddress">Où <see cref="SetColorFilterSignature"/> a été trouvée.</param>
    /// <param name="readCode">Lit des octets du code du jeu ; null si l'adresse sort de la section de code.</param>
    /// <param name="expectedOverrideOffset">La position de ColorFilterOverride dans EnvManager selon ClientStructs.</param>
    /// <param name="envManagerSize">La taille d'EnvManager selon ClientStructs.</param>
    /// <param name="error">Pourquoi ça n'a pas marché (pour le journal), sinon null.</param>
    public static GameAddresses? Resolve(
        nint siteAddress,
        Func<nint, int, byte[]?> readCode,
        int expectedOverrideOffset,
        int envManagerSize,
        out string? error)
    {
        var site = readCode(siteAddress, SetColorFilterLength);
        if (site == null || !Matches(site, SetColorFilterSignature))
        {
            error = "SetColorFilter does not match the expected code.";
            return null;
        }

        int argumentOffset = BinaryPrimitives.ReadInt32LittleEndian(site.AsSpan(ArgumentDisp));
        int overrideOffset = BinaryPrimitives.ReadInt32LittleEndian(site.AsSpan(OverrideDisp));
        int ownerOffset = BinaryPrimitives.ReadInt32LittleEndian(site.AsSpan(OwnerDisp));
        nint reserveFunction = siteAddress + CallEnd + BinaryPrimitives.ReadInt32LittleEndian(site.AsSpan(CallRel));

        if (overrideOffset != expectedOverrideOffset)
        {
            error = $"ColorFilterOverride is at 0x{overrideOffset:X} in the game code, 0x{expectedOverrideOffset:X} in ClientStructs.";
            return null;
        }

        var reserve = readCode(reserveFunction, ReserveLength);
        if (reserve == null || !Matches(reserve, ReserveSignature))
        {
            error = $"The reservation function (0x{reserveFunction:X}) does not match the expected code.";
            return null;
        }

        var ownersArray = OwnersArrayDisps.Select(p => BinaryPrimitives.ReadInt32LittleEndian(reserve.AsSpan(p))).Distinct().ToArray();
        if (ownersArray.Length != 1)
        {
            error = "The reservation function reads and writes different owner bytes.";
            return null;
        }

        // L'octet comparé par SetColorFilter doit être celui que la fonction de réservation écrit pour l'emplacement 15.
        if (argumentOffset + ownersArray[0] + Slot != ownerOffset)
        {
            error = $"Owner byte mismatch: 0x{argumentOffset:X} + 0x{ownersArray[0]:X} + {Slot} != 0x{ownerOffset:X}.";
            return null;
        }

        if (ownerOffset <= 0 || ownerOffset >= envManagerSize || argumentOffset <= 0 || argumentOffset >= envManagerSize)
        {
            error = $"Offsets outside EnvManager (0x{argumentOffset:X}, 0x{ownerOffset:X}, size 0x{envManagerSize:X}).";
            return null;
        }

        error = null;
        return new GameAddresses(reserveFunction, argumentOffset, ownerOffset);
    }

    /// <summary>
    /// Lecteur du code du jeu pour <see cref="Resolve"/>. Les adresses données sont les vraies (celles que rend
    /// ScanAllText) ; la lecture se fait dans la copie que garde Dalamud (searchBase = TextSectionBase - TextSectionOffset,
    /// son .text est relu dans ffxiv_dx11.exe : sans les crochets des autres plugins), et jamais hors du .text.
    /// </summary>
    public static Func<nint, int, byte[]?> CodeReader(
        nint moduleBase, long textOffset, int textSize, nint searchBase, Func<nint, int, byte[]> readMemory)
    {
        nint textStart = moduleBase + (nint)textOffset;
        nint textEnd = textStart + textSize;
        nint copyDelta = searchBase - moduleBase;
        return (address, length) =>
            length > 0 && address >= textStart && address <= textEnd - length
                ? readMemory(address + copyDelta, length)
                : null;
    }

    /// <summary>Les octets correspondent au motif (octets en hexadécimal, « ?? » pour n'importe quel octet).</summary>
    public static bool Matches(ReadOnlySpan<byte> code, string pattern)
    {
        var parts = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (code.Length < parts.Length)
            return false;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] != "??" && code[i] != Convert.ToByte(parts[i], 16))
                return false;
        }

        return true;
    }

    private static int PatternLength(string pattern) => pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}
