using System.Collections.Generic;
using System.Linq;

namespace GposeFilterXIV;

/// <summary>
/// La liste des filtres telle que GPose la montre : les lignes de la feuille ColorFilter dont le rang (SortOrder)
/// n'est pas 0, triées par rang (lu dans l'agent GPose du jeu, 0x14100EAC0 en version 2026.11.03).
/// La ligne 0 (« Standard ») en fait partie : la choisir revient à ne pas mettre de filtre, comme dans GPose.
/// </summary>
internal sealed class FilterCatalog
{
    /// <summary>La ligne 0 : pas de filtre (GPose libère l'emplacement quand on la choisit).</summary>
    public const uint StandardId = 0;

    private readonly Dictionary<uint, FilterRow> byId;

    public FilterCatalog(IEnumerable<FilterRow> rows)
    {
        Entries = rows
            .Where(r => r.SortOrder != 0)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Id)
            .ToList();
        byId = Entries.ToDictionary(r => r.Id);
    }

    /// <summary>Les filtres dans l'ordre de GPose.</summary>
    public IReadOnlyList<FilterRow> Entries { get; }

    /// <summary>Le filtre de cette ligne, s'il fait partie de la liste de GPose.</summary>
    public bool TryGet(uint id, out FilterRow row) => byId.TryGetValue(id, out row!);
}
