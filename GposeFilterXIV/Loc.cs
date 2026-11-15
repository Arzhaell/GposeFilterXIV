namespace GposeFilterXIV;

public enum PluginLanguage
{
    /// <summary>Suit la langue du jeu (celle des noms de filtres).</summary>
    Auto,
    English,
    French,
}

/// <summary>Traductions : chaque texte est écrit en anglais et en français à l'endroit où il est utilisé.</summary>
internal static class Loc
{
    public static bool IsFrench { get; private set; }

    /// <param name="language">Le réglage du plugin.</param>
    /// <param name="gameIsFrench">Le jeu est en français (pour « Automatique »).</param>
    public static void Update(PluginLanguage language, bool gameIsFrench) => IsFrench = language switch
    {
        PluginLanguage.French => true,
        PluginLanguage.English => false,
        _ => gameIsFrench,
    };

    public static string T(string english, string french) => IsFrench ? french : english;
}
