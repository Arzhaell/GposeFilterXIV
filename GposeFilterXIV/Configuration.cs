using System;
using Dalamud.Configuration;

namespace GposeFilterXIV;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Garder le filtre choisi hors GPose.</summary>
    public bool Enabled { get; set; }

    /// <summary>La ligne de la feuille ColorFilter choisie (0 = « Standard », pas de filtre).</summary>
    public uint FilterId { get; set; }

    /// <summary>L'intensité, de 0 à 100 % comme le curseur de GPose.</summary>
    public int Intensity { get; set; } = 100;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
