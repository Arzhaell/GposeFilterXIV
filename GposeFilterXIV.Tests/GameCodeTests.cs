namespace GposeFilterXIV.Tests;

/// <summary>Lecture des adresses dans le code du jeu, sur les vrais octets de ffxiv_dx11.exe 2026.11.03.</summary>
public class GameCodeTests
{
    private static readonly nint SiteAddress = unchecked((nint)0x1417DC234);
    private static readonly nint ReserveAddress = unchecked((nint)0x140066E50);
    private const int OverrideOffset = 0x3D0;
    private const int EnvManagerSize = 0xBF0;

    // ffxiv_dx11.exe 2026.11.03, 0x1417DC234 (dans SetColorFilter).
    private static byte[] Site() =>
    [
        0x41, 0xB1, 0x40, 0x48, 0x8B, 0x1D, 0x62, 0xF2, 0x12, 0x01, 0x41, 0xB0, 0x01, 0xBA, 0x0F, 0x00,
        0x00, 0x00, 0x48, 0x8D, 0x8B, 0x58, 0x03, 0x00, 0x00, 0xE8, 0xFE, 0xAB, 0x88, 0xFE, 0x33, 0xC9,
        0x48, 0x8D, 0x83, 0xD0, 0x03, 0x00, 0x00, 0x80, 0xBB, 0x5B, 0x04, 0x00, 0x00, 0x40,
    ];

    // ffxiv_dx11.exe 2026.11.03, 0x140066E50 (fonction de réservation).
    private static byte[] Reserve() =>
    [
        0x48, 0x63, 0xC2, 0x44, 0x0F, 0xB6, 0x94, 0x08, 0xF4, 0x00, 0x00, 0x00, 0x45, 0x84, 0xC0, 0x74,
        0x0E, 0x45, 0x3A, 0xD1, 0x73, 0x25, 0x44, 0x88, 0x8C, 0x08, 0xF4, 0x00, 0x00, 0x00, 0xC3, 0x45,
        0x3A, 0xD1, 0x75, 0x17, 0xC6, 0x84, 0x08, 0xF4, 0x00, 0x00, 0x00, 0x00, 0x45, 0x33, 0xC0, 0x48,
        0x8B, 0x0D, 0x12, 0x46, 0x8A, 0x02, 0xE9, 0xE5, 0x52, 0x2F, 0x00, 0xC3,
    ];

    /// <summary>Un faux code du jeu : seulement ces deux morceaux, null ailleurs (hors de la section de code).</summary>
    private static Func<nint, int, byte[]?> Code(byte[] site, byte[] reserve) => (address, length) =>
        address == SiteAddress && length <= site.Length ? site[..length]
        : address == ReserveAddress && length <= reserve.Length ? reserve[..length]
        : null;

    private static GameAddresses? Resolve(byte[] site, byte[] reserve, out string? error, int overrideOffset = OverrideOffset, int size = EnvManagerSize) =>
        GameCode.Resolve(SiteAddress, Code(site, reserve), overrideOffset, size, out error);

    [Fact]
    public void ReadsTheAddressesOfThisGameVersion()
    {
        var addresses = Resolve(Site(), Reserve(), out var error);

        Assert.Null(error);
        Assert.NotNull(addresses);
        Assert.Equal(ReserveAddress, addresses.ReserveFunction);
        Assert.Equal(0x358, addresses.ReserveArgumentOffset);
        Assert.Equal(0x45B, addresses.OwnerOffset);
    }

    [Fact]
    public void PatternsHaveTheExpectedLength()
    {
        Assert.Equal(Site().Length, GameCode.SetColorFilterLength);
        Assert.Equal(Reserve().Length, GameCode.ReserveLength);
        Assert.True(GameCode.Matches(Site(), GameCode.SetColorFilterSignature));
        Assert.True(GameCode.Matches(Reserve(), GameCode.ReserveSignature));
    }

    [Fact]
    public void OffsetsThatMovedTogetherAreFollowed()
    {
        // Une mise à jour qui décale EnvManager de 0x10 sans rien changer d'autre : on suit.
        var site = Site();
        site[21] = 0x68; // 0x358 → 0x368
        site[35] = 0xE0; // 0x3D0 → 0x3E0
        site[41] = 0x6B; // 0x45B → 0x46B

        var addresses = Resolve(site, Reserve(), out var error, overrideOffset: 0x3E0);

        Assert.Null(error);
        Assert.Equal(0x368, addresses!.ReserveArgumentOffset);
        Assert.Equal(0x46B, addresses.OwnerOffset);
    }

    [Fact]
    public void OverrideElsewhereThanClientStructsPauses()
    {
        Assert.Null(Resolve(Site(), Reserve(), out var error, overrideOffset: 0x3E0));
        Assert.Contains("ColorFilterOverride", error);
    }

    [Fact]
    public void OwnerByteThatIsNotSlot15Pauses()
    {
        var site = Site();
        site[41] = 0x5C; // cmp byte ptr [rbx+0x45C] : un autre emplacement

        Assert.Null(Resolve(site, Reserve(), out var error));
        Assert.Contains("Owner byte", error);
    }

    [Fact]
    public void OtherSlotDoesNotMatch()
    {
        var site = Site();
        site[14] = 0x0E; // mov edx, 0xE

        Assert.False(GameCode.Matches(site, GameCode.SetColorFilterSignature));
        Assert.Null(Resolve(site, Reserve(), out _));
    }

    [Fact]
    public void ChangedReservationRulePauses()
    {
        var reserve = Reserve();
        reserve[20] = 0x72; // jb au lieu de jae : le plus petit gagnerait

        Assert.Null(Resolve(Site(), reserve, out var error));
        Assert.Contains("reservation function", error);
    }

    [Fact]
    public void ReservationReadingOneByteAndWritingAnotherPauses()
    {
        var reserve = Reserve();
        reserve[26] = 0xF5; // écrit à +0xF5, lit à +0xF4

        Assert.Null(Resolve(Site(), reserve, out var error));
        Assert.Contains("different owner bytes", error);
    }

    [Fact]
    public void CallOutsideTheCodeSectionPauses()
    {
        var site = Site();
        site[29] = 0x7E; // l'appel part ailleurs : le faux code n'a rien à cette adresse

        Assert.Null(Resolve(site, Reserve(), out var error));
        Assert.Contains("reservation function", error);
    }

    [Fact]
    public void OffsetsOutsideEnvManagerPause()
    {
        Assert.Null(Resolve(Site(), Reserve(), out var error, size: 0x400));
        Assert.Contains("outside EnvManager", error);
    }

    [Fact]
    public void UnreadableSitePauses()
    {
        Assert.Null(GameCode.Resolve(SiteAddress, (_, _) => null, OverrideOffset, EnvManagerSize, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void CodeIsReadInDalamudsCopyWithinTheRealTextSection()
    {
        // Module chargé en 0x140000000, .text à +0x1000 sur 0x100 octets, copie de Dalamud ailleurs.
        nint module = unchecked((nint)0x140000000);
        nint copy = unchecked((nint)0x7FF000000000);
        var reads = new List<nint>();
        var read = GameCode.CodeReader(module, 0x1000, 0x100, copy, (address, length) =>
        {
            reads.Add(address);
            return new byte[length];
        });

        Assert.NotNull(read(module + 0x1010, 16));
        Assert.Equal(copy + 0x1010, reads.Single());     // lu dans la copie
        Assert.NotNull(read(module + 0x10F0, 16));        // jusqu'au dernier octet du .text
        Assert.Null(read(module + 0x10F1, 16));           // déborde du .text
        Assert.Null(read(module + 0xFF0, 16));            // avant le .text
        Assert.Null(read(copy + 0x1010, 16));             // une adresse de la copie n'est pas une vraie adresse
        Assert.Equal(2, reads.Count);

        // Sans copie, SearchBase est le module lui-même.
        var direct = GameCode.CodeReader(module, 0x1000, 0x100, module, (address, length) =>
        {
            reads.Add(address);
            return new byte[length];
        });
        Assert.NotNull(direct(module + 0x1010, 16));
        Assert.Equal(module + 0x1010, reads.Last());
    }

    [Fact]
    public void MatchesHandlesWildcardsAndShortCode()
    {
        Assert.True(GameCode.Matches([0x48, 0x12, 0xC3], "48 ?? C3"));
        Assert.False(GameCode.Matches([0x48, 0x12, 0xC2], "48 ?? C3"));
        Assert.False(GameCode.Matches([0x48, 0x12], "48 ?? C3"));
    }
}
