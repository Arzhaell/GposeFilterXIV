namespace GposeFilterXIV;

/// <summary>Comment afficher la ligne d'état de la fenêtre.</summary>
internal enum StatusKind
{
    /// <summary>Rien à dire (plugin désactivé).</summary>
    None,

    /// <summary>Le filtre est en place.</summary>
    Ok,

    /// <summary>Pour information (Standard, en attente, GPose passe devant).</summary>
    Info,

    /// <summary>Quelque chose à corriger par l'utilisateur.</summary>
    Warning,

    /// <summary>Le plugin ne peut rien faire.</summary>
    Error,
}

/// <summary>La ligne d'état de la fenêtre : ce que le plugin fait en ce moment, en une phrase.</summary>
internal static class FilterStatus
{
    /// <summary>Message du chat et de la fenêtre quand le code du jeu n'est plus celui qu'on connaît.</summary>
    public static string PausedText => Loc.T(
        "Paused: the game changed since this version of GposeFilterXIV (update?). It leaves the filters alone until a new version comes out.",
        "En pause : le jeu a changé depuis cette version de GposeFilterXIV (mise à jour ?). Il ne touche plus aux filtres en attendant une nouvelle version.");

    public static (StatusKind Kind, string Text) For(bool paused, bool enabled, uint filterId, bool filterListed, SlotState state, byte owner)
    {
        if (paused)
            return (StatusKind.Error, PausedText);
        if (!enabled)
            return (StatusKind.None, string.Empty);
        if (filterId == FilterCatalog.StandardId)
            return (StatusKind.Info, Loc.T("Standard: no filter, like in GPose.", "Standard : pas de filtre, comme dans GPose."));
        if (!filterListed)
            return (StatusKind.Warning, Loc.T(
                "This filter is no longer in the game: pick another one.",
                "Ce filtre n'existe plus dans le jeu : choisis-en un autre."));

        return state switch
        {
            SlotState.Holding => (StatusKind.Ok, Loc.T("Filter on.", "Filtre appliqué.")),
            SlotState.Yielded when owner == FilterSlot.SceneOwner => (StatusKind.Info, Loc.T(
                "GPose or a cutscene is using its own filter: yours comes back afterwards.",
                "GPose ou une cinématique utilise son propre filtre : le tien revient ensuite.")),
            SlotState.Yielded => (StatusKind.Info, Loc.T(
                "Another game effect is using the filter: yours comes back afterwards.",
                "Un autre effet du jeu utilise le filtre : le tien revient ensuite.")),
            _ => (StatusKind.Info, Loc.T("Waiting for the game…", "En attente du jeu…")),
        };
    }
}
