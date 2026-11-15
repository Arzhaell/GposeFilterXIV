using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;

namespace GposeFilterXIV;

/// <summary>L'emplacement du filtre de couleur dans le jeu (le vrai, ou un faux pour les tests).</summary>
internal interface IFilterSlot
{
    /// <summary>Qui tient l'emplacement : 0 personne, 0x40 GPose / scènes, <see cref="FilterSlot.OurOwner"/> nous.</summary>
    byte Owner { get; }

    /// <summary>Réserve (le plus grand propriétaire gagne) ou libère (si c'est encore nous) l'emplacement, avec notre propriétaire.</summary>
    void Reserve(bool enable);

    /// <summary>Écrit les valeurs dans EnvManager.ColorFilterOverride.</summary>
    void Write(in EnvColorFilterParameters values);
}

/// <summary>Ce que l'on fait de l'emplacement à cette image.</summary>
internal enum SlotAction
{
    /// <summary>Rien : on n'en veut pas et ce n'est pas nous, ou quelqu'un d'autre le tient.</summary>
    None,

    /// <summary>Libre et on veut un filtre : le réserver puis écrire.</summary>
    Take,

    /// <summary>C'est déjà nous : écrire (au cas où quelqu'un aurait changé les valeurs).</summary>
    Write,

    /// <summary>C'est nous et on n'en veut plus : remettre à zéro puis libérer (le filtre de zone revient).</summary>
    Release,
}

/// <summary>Ce qu'on a constaté à cette image (pour le journal et la fenêtre).</summary>
internal enum SlotState
{
    /// <summary>Pas d'EnvManager (écran de chargement) : rien lu, rien écrit.</summary>
    Unavailable,

    /// <summary>Pas de filtre voulu, l'emplacement n'est pas à nous.</summary>
    Idle,

    /// <summary>Notre filtre est en place.</summary>
    Holding,

    /// <summary>On veut un filtre, mais l'emplacement est tenu par quelqu'un d'autre (GPose, cinématique…).</summary>
    Yielded,
}

internal static class FilterSlot
{
    /// <summary>
    /// Notre propriétaire. Plus petit que 0x40 (GPose, scènes) et 0x80 (module du jeu) : ils passent devant.
    /// Personne d'autre ne prend l'emplacement 15 dans le jeu (version 2026.11.03).
    /// </summary>
    public const byte OurOwner = 0x20;

    /// <summary>Le propriétaire des scènes d'événement : GPose, cinématiques, IdleCam.</summary>
    public const byte SceneOwner = 0x40;

    /// <summary>
    /// La règle : on ne prend l'emplacement que s'il est libre, on n'écrit que s'il est à nous, on ne rend que le nôtre.
    /// Un propriétaire étranger, plus grand ou plus petit que nous, n'est jamais touché.
    /// </summary>
    public static SlotAction Decide(byte owner, bool wanted) => (wanted, owner) switch
    {
        (true, 0) => SlotAction.Take,
        (true, OurOwner) => SlotAction.Write,
        (false, OurOwner) => SlotAction.Release,
        _ => SlotAction.None,
    };

    /// <summary>Une image : applique la règle à l'emplacement (null = pas d'EnvManager, on ne fait rien).</summary>
    public static SlotState Update(IFilterSlot? slot, EnvColorFilterParameters? wanted)
    {
        if (slot == null)
            return SlotState.Unavailable;

        switch (Decide(slot.Owner, wanted.HasValue))
        {
            case SlotAction.Take:
                slot.Reserve(true);
                if (slot.Owner != OurOwner)
                    return SlotState.Yielded;
                slot.Write(wanted!.Value);
                return SlotState.Holding;

            case SlotAction.Write:
                slot.Write(wanted!.Value);
                return SlotState.Holding;

            case SlotAction.Release:
                Release(slot);
                return SlotState.Idle;

            default:
                return wanted.HasValue ? SlotState.Yielded : SlotState.Idle;
        }
    }

    /// <summary>Rend l'emplacement s'il est à nous (désactivation, déchargement du plugin), comme le jeu quand il quitte GPose.</summary>
    public static void Release(IFilterSlot? slot)
    {
        if (slot == null || slot.Owner != OurOwner)
            return;
        slot.Write(FilterValues.Identity);
        slot.Reserve(false);
    }
}
