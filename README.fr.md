# GposeFilterXIV

*[English version](README.md)*

Un plugin Dalamud pour FINAL FANTASY XIV qui garde un des filtres de couleur de la GPose hors de la GPose.

## Utilisation
Tape `/gposefilter` (ou ouvre le plugin depuis `/xlplugins`) : une fenêtre faite des éléments d'interface du jeu
s'ouvre.

- **Activé** : garder le filtre.
- **Filtre** : la liste de la GPose, dans le même ordre et dans la langue du jeu. « Standard » veut dire pas de filtre,
  comme dans la GPose.
- **Intensité** : de 0 à 100 %, comme le curseur de la GPose. Comme dans la GPose, 0 % veut dire aucun filtre du tout
  (le filtre propre à la zone est caché lui aussi).

Le filtre reste partout, et revient tout seul après les écrans de chargement et quand tu te reconnectes. Une ligne de la
fenêtre dit ce que fait le plugin.

## Comment ça marche
C'est le jeu qui dessine le filtre, exactement comme dans la GPose : le plugin écrit les valeurs du filtre choisi (tirées
de la feuille `ColorFilter` du jeu) là où la GPose les écrit, et réserve cette place avec la fonction du jeu. Pas de
shader, pas de hook, aucun calcul de couleur maison pour le filtre.

La fenêtre est construite avec [KamiToolKit](https://github.com/MidoriKami/KamiToolKit) à partir des éléments
d'interface du jeu. Pour qu'elle se ferme avec les fenêtres du jeu (Échap), KamiToolKit pose un hook sur la fonction du
jeu qui ferme les fenêtres : c'est le seul, et il n'a rien à voir avec le filtre.

- **La GPose et les cinématiques passent devant** : quand elles mettent leur propre filtre, le plugin s'efface, puis le
  tien revient. Dans la GPose avec « Standard », le tien reste.
- **Le désactiver** ou décharger le plugin rend le filtre propre à la zone.
- **Après une mise à jour du jeu**, le plugin vérifie au démarrage que le code du jeu est toujours celui qu'il connaît.
  Sinon, il se met en pause (il le dit dans la fenêtre et une fois dans le chat) et n'écrit plus rien en attendant une
  nouvelle version.

Disponible en anglais et en français (suit la langue du jeu, réglable dans la fenêtre).

## Installation
Ajoute ce dépôt personnalisé dans `/xlsettings`, onglet **Experimental**, puis installe GposeFilterXIV depuis
`/xlplugins` :

```
https://raw.githubusercontent.com/Arzhaell/GposeFilterXIV/main/repo.json
```

## Compilation
SDK .NET 10 et XIVLauncher (Dalamud) installés. Clone avec le sous-module KamiToolKit :

```bash
git clone --recurse-submodules https://github.com/Arzhaell/GposeFilterXIV.git
```

```bash
dotnet build -c Release
```

```bash
dotnet test GposeFilterXIV.Tests/GposeFilterXIV.Tests.csproj
```

Certains tests lisent le jeu installé (en lecture seule) : la feuille `ColorFilter`, et `ffxiv_dx11.exe` pour vérifier
que le plugin trouve toujours le code dont il a besoin. Lance-les après une mise à jour du jeu pour savoir si le plugin
marche encore, sans lancer le jeu. Sur une machine sans le jeu, mets `GPOSEFILTER_TESTS_WITHOUT_GAME=1` : ces tests ne
vérifient alors rien.

Plugin de dev : ajoute `GposeFilterXIV\bin\Debug\GposeFilterXIV.dll` aux emplacements des plugins de dev
(`/xlsettings`, onglet **Experimental**) ; Dalamud le recharge tout seul après chaque compilation, même jeu ouvert. Il
ne charge pas en même temps la version installée depuis le dépôt : désactive-la.

## Publier une nouvelle version
Il suffit de pousser un tag annoté `vX.Y.Z` : le workflow GitHub lance les tests, compile le plugin, crée la release
(ses notes sont le message du tag) et met à jour `repo.json`.

```bash
git tag -a v1.0.0 --cleanup=verbatim -F notes.txt
```

```bash
git push origin v1.0.0
```

## Crédits
- [Dalamud](https://github.com/goatcorp/Dalamud) (AGPL-3.0) : chargement du plugin, accès au jeu.
- [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) (MIT) : structures du jeu (`EnvManager`, son
  `ColorFilterOverride`).
- [Lumina](https://github.com/NotAdam/Lumina) (WTFPL) : lecture des données du jeu (feuille `ColorFilter`).
- [EXDSchema](https://github.com/xivdev/EXDSchema) : noms des colonnes de la feuille `ColorFilter`.
- [KamiToolKit](https://github.com/MidoriKami/KamiToolKit) (MIT, sous-module git) : la fenêtre, faite des éléments
  d'interface du jeu.
- [Dear ImGui](https://github.com/ocornut/imgui) (MIT) : la bibliothèque d'interface utilisée par Dalamud.
- [xUnit](https://xunit.net/) pour les tests.

GposeFilterXIV est publié sous licence **AGPL-3.0** (voir `LICENSE`), comme Dalamud.

FINAL FANTASY XIV © SQUARE ENIX CO., LTD. Ce plugin n'est ni affilié à Square Enix ni approuvé par Square Enix.
