# Ravenne's Tools Menu 🎡

**Une roue d'outils contextuelle pour Windows** — maintenez le bouton du milieu de la souris, glissez vers une option, relâchez pour l'exécuter. Les sous-menus s'ouvrent au passage du curseur, et le contenu de la roue s'adapte automatiquement à ce que vous êtes en train de faire (fichiers sélectionnés, texte, application active).

- **Technologie** : C# / WPF (.NET 9), zéro dépendance externe
- **Consommation** : 0 % CPU au repos, fenêtre pré-créée, rendu uniquement à la demande (~35 Mo de RAM)
- **Système** : Windows 10 1809+ / Windows 11, x64

---

## Sommaire

1. [Installation](#installation)
2. [Utilisation](#utilisation)
3. [Les roues par défaut](#les-roues-par-défaut)
4. [Configuration (config.json)](#configuration-configjson)
5. [Types d'actions](#types-dactions)
6. [Détection de contexte](#détection-de-contexte)
7. [Performances](#performances)
8. [Générer l'installateur](#générer-linstallateur)
9. [Dépannage](#dépannage)

---

## Installation

### Depuis l'installateur (recommandé)

1. Compilez puis lancez `installer/RavenneToolsMenu-Setup.exe` (voir [Générer l'installateur](#générer-linstallateur)), ou récupérez l'installateur fourni.
2. L'application démarre avec Windows et s'installe dans la barre des tâches (icône de roue violette).

L'installateur nécessite le **runtime .NET 9 Desktop** — il vous proposera de le télécharger s'il manque.

### Depuis les sources

```bash
# Prérequis : SDK .NET 9 (https://dotnet.microsoft.com/download/dotnet/9.0)
cd src/RavenneToolsMenu
dotnet run -c Release
```

Publication en un seul fichier :

```bash
dotnet publish src/RavenneToolsMenu -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

---

## Utilisation

| Geste | Effet |
|---|---|
| **Maintenir le bouton du milieu + glisser + relâcher** | Ouvre la roue, sélectionne, exécute |
| Glisser vers une option marquée de 3 points | Ovre son sous-menu (la roue se recentre dessus) |
| Ramener le curseur au centre d'un sous-menu (~0,1 s) | Revient au menu parent |
| Clic molette **bref** sans glisser | Transmis normalement à l'application (défilement, etc.) |
| Relâcher au centre de la roue racine | Ferme sans rien faire |

Autres déclencheurs disponibles (clic droit de l'icône → *Déclencheur*) : molette, Ctrl/Maj/Alt + molette, bouton latéral avant, ou **clic droit long** (~0,3 s ; un clic droit bref garde le menu Windows normal).

Le mode **plein écran est ignoré par défaut** (jeux) et les applications de la liste `disabledProcesses` (mstsc, AnyDesk…) ne déclenchent jamais la roue.

> 💡 Test rapide : double-clic sur l'icône de la barre des tâches → la roue s'ouvre en mode « clic gauche = valider ».

---

## Les roues par défaut

| Contexte | Contenu |
|---|---|
| **Partout** (principal) | Navigateur, Terminal, Bloc-notes, Média (6 commandes), Presse-papiers, Système (verrouiller, capture, corbeille…), Outils (IP publique, DNS…) |
| **Fichiers sélectionnés dans l'Explorateur** | Copier chemins/noms, ZIP, Ouvrir avec…, Convertir (PNG/JPG/BMP/GIF/TIFF/ICO/MP3/MP4), Terminal ici, Recycler |
| **Images sélectionnées** | Convertir, Redimensionner (25 %, 50 %, 1024/512 px), Compresser JPEG, Copier chemin, ZIP, Recycler |
| **Texte sélectionné** | Copier, Google, Traduire, Wikipédia, MAJUSCULES/minuscules/Capitaliser (remplace la sélection), Coller sans formatage |
| **Navigateur web** (Chrome, Edge, Firefox…) | Nouvel onglet, Rouvrir fermé, Fenêtre privée, Téléchargements, Historique, Zoom |

La conversion **MP3/MP4** utilise *ffmpeg* s'il est installé (voir [Dépannage](#dépannage)). Les conversions d'images sont natives (aucune dépendance) et respectent l'orientation EXIF des photos.

---

## Configuration (config.json)

Fichier : `%APPDATA%\RavenneToolsMenu\config.json` — éditable avec le Bloc-notes, **rechargé automatiquement** à l'enregistrement (les commentaires `//` sont acceptés). Bouton *Ouvrir la configuration* dans le menu de l'icône.

### Structure

```jsonc
{
  "version": 1,
  "trigger": {
    "button": "middle",            // middle | ctrl+middle | shift+middle | alt+middle | xbutton2 | right-long
    "longPressMs": 320,            // durée du « clic droit long »
    "quickClickMs": 170,           // clic bref = transmis à l'appli
    "quickClickPassthrough": true
  },
  "appearance": {
    "accent": "#7C4DFF",           // couleur d'accent
    "radius": 150,                 // taille de la roue (100–220)
    "iconSize": 26,
    "animations": true,
    "showItemLabels": true,
    "opacity": 0.97
  },
  "ffmpeg": { "path": "ffmpeg" },  // "ffmpeg" (PATH) ou chemin complet
  "skipFullscreen": true,          // ne pas déclencher dans les jeux plein écran
  "disabledProcesses": ["mstsc"],  // processus exclus
  "rules": [ /* voir plus bas */ ],
  "menus": { /* voir plus bas */ ]
}
```

### Règles (choix de la roue selon le contexte)

Les règles sont évaluées **dans l'ordre** ; la première qui correspond gagne :

```jsonc
"rules": [
  {
    "name": "Images sélectionnées",
    "match": {
      "processes": [],                    // noms de processus (optionnel)
      "selectedFiles": true,              // des fichiers sont sélectionnés
      "fileExtensions": [".png", ".jpg"], // …avec l'une de ces extensions
      "selectedText": false               // du texte est sélectionné
    },
    "menu": "images"                      // roue à afficher
  }
]
```

Tous les critères présents doivent être vrais simultanément. Sans règle active, la roue `principal` s'affiche.

### Menus et options

```jsonc
"menus": {
  "principal": {
    "label": "Ravenne's Tools",
    "items": [
      { "label": "Navigateur", "icon": "globe", "action": { "kind": "url", "url": "https://google.com" } },
      {
        "label": "Média", "icon": "music", "description": "Lecture et volume",
        "items": [                          // → sous-menu
          { "label": "Lecture / Pause", "icon": "play", "action": { "kind": "media", "media": "playpause" } }
        ]
      }
    ]
  }
}
```

- `icon` : nom d'icône intégrée (`copy`, `globe`, `terminal`, `image`, `zip`, `convert`… ~50 icônes), **emoji** (`"🔥"`), chemin d'image, ou vide (lettre du label).
- `description` : petite aide affichée au centre quand l'option est survolée.
- `toast` : message affiché après exécution.
- Les gabarits d'URL/arguments acceptent `{text}` (texte sélectionné), `{file}` et `{dir}` (1er fichier). `{text}` est encodé pour les URL.

---

## Types d'actions

| `kind` | Champs | Effet |
|---|---|---|
| `launch` | `path`, `args` | Lance un programme/dossier (`ms-settings:` accepté) |
| `url` | `url` | Ouvre une URL (`{text}` accepté) |
| `keys` | `keys` | Simule un raccourci (`"ctrl+shift+t"`, `"win+d"`…) |
| `media` | `media` | `playpause` `next` `prev` `volup` `voldn` `mute` |
| `system` | `system` | `lock` `taskmgr` `screenshot` `showdesktop` `emptytrash` `opentrash` `sleep` `settings` `signout` `clipboardClear` |
| `script` | `script`, `showWindow`, `toastResult` | Exécute PowerShell (sortie en toast si `toastResult`) |
| `convertImage` | `convertTo`, `quality` | PNG/JPG/BMP/GIF/TIFF/ICO (codecs natifs, EXIF respecté) |
| `resizeImage` | `scale` ou `maxWidth` | Redimensionne (25 %, 1024 px…) en gardant le format |
| `ffmpeg` | `convertTo`, `ffmpegArgs` | MP3/MP4/WAV/WebM via ffmpeg (`{input}` `{output}` dans le gabarit) |
| `zip` | — | Compresse la sélection |
| `copyPath` | `mode` | `full` (chemins) ou `names` (noms) |
| `openWith` | — | Boîte « Ouvrir avec » |
| `openTerminalHere` | — | Windows Terminal (repli cmd) dans le dossier |
| `recycle` | — | Envoie à la corbeille |
| `textTransform` | `textTransform` | `upper` `lower` `title` `trim` — remplace la sélection |
| `reloadConfig` | — | Recharge config.json |
| `openConfigFolder` | — | Ouvre `%APPDATA%\RavenneToolsMenu` |

Chaque action accepte `delayMs` (pause avant exécution, par défaut 40 ms).

---

## Détection de contexte

- **Application active** : fiable à 100 % (nom du processus au premier plan).
- **Fichiers sélectionnés (Explorateur)** : lecture directe via les interfaces COM du Shell — chemins complets, ~10–50 ms.
- **Texte sélectionné** : via UI Automation (fonctionne dans la plupart des applications natives ; dans les navigateurs, dépend de l'accessibilité activée — Chrome : `chrome://accessibility`). Repli : les transformations de texte font un Ctrl+C masqué si la sélection n'est pas détectable.
- **Lien / image sous le curseur** : expérimental (UI Automation), exposé pour de futures règles.

La roue s'ouvre **immédiatement** avec le contexte rapide (application), puis se remplace par la roue contextuelle si la détection complète arrive avant que vous ayez commencé à naviguer.

---

## Performances

Conçu pour être invisible quand on ne l'utilise pas :

- **aucun timer, aucune boucle** au repos ; seul le hook souris bas niveau tourne (coût négligeable) ;
- fenêtre overlay **créée une fois** à froid puis simplement affichée/masquée → ouverture quasi instantanée ;
- rendu en un seul `DrawingContext` avec pinceaux/textures **gelés** (freeze) — redraw uniquement pendant animation ou changement de sélection ;
- icônes **vectorielles maison** (~50, dessinées en code) : aucun asset, aucune bitmap ;
- détection de contexte sur un **thread STA dédié** pour ne jamais bloquer l'interface.

---

## Générer l'installateur

Prérequis : [Inno Setup 6](https://jrsoftware.org/isinfo.php) et le SDK .NET 9.

```bash
# 1. Publier l'application
dotnet publish src/RavenneToolsMenu -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish

# 2. Compiler l'installateur (adapter le chemin d'Inno Setup)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer/RavenneToolsMenu.iss
```

L'installateur `installer/Output/RavenneToolsMenu-Setup.exe` propose : dossier, raccourci menu Démarrer, icône bureau, **démarrage automatique**, téléchargement du runtime .NET 9 si absent.

---

## Mises à jour automatiques & GitHub

Le dépôt : **https://github.com/ravennelucas1908-max/RavenneToolsMenu**

### Comment ça marche

1. **Chaque version** est publiée en poussant un tag : `powershell scripts/new-release.ps1 -Version 1.1.0` (bump des numéros de version, commit, tag `v1.1.0`, push).
2. Le workflow GitHub Actions **Release** (`.github/workflows/release.yml`) compile automatiquement l'installateur sur les serveurs GitHub, vérifie la cohérence tag ↔ version, et publie la **release** avec `RavenneToolsMenu-Setup.exe` en pièce jointe.
3. **Côté utilisateur**, l'application vérifie les mises à jour au démarrage (1 fois / 24 h, désactivable) et via *clic droit icône → Vérifier les mises à jour…*. Si une nouvelle version existe : fenêtre avec les notes de version, téléchargement avec progression, puis relance de l'installateur (l'application se ferme, l'installateur remplace les fichiers et propose de relancer).
4. Réglages dans `config.json` → `updates` : `repo` (dépôt source), `enabled` (vérification auto), `skipVersion` (version ignorée).

> La première installation se fait depuis la page **Releases** du dépôt (`RavenneToolsMenu-Setup.exe`). Les mises à jour suivantes sont proposées directement dans l'application.

### CI

À chaque push sur `main`, le workflow **CI** (`.github/workflows/ci.yml`) compile, publie et valide la configuration (mode `--validate`), et expose l'exe portable en artifact.

---

## Dépannage

| Problème | Solution |
|---|---|
| La molette n'ouvre rien | Vérifiez que l'icône est dans la barre des tâches ; désactivez « Désactivé en plein écran » si vous êtes dans un jeu ; regardez `%APPDATA%\RavenneToolsMenu\logs\ravenne.log` |
| La roue s'ouvre dans un jeu | Ajoutez le jeu à `disabledProcesses`, ou gardez `skipFullscreen: true` |
| Conversion MP3/MP4 échoue | Installez ffmpeg (`winget install Gyan.FFmpeg`) ou indiquez son chemin complet dans `ffmpeg.path` |
| WebP non converti | Le codec WebP n'est pas fourni par Windows ; convertissez via ffmpeg |
| Antivirus méfiant du hook | C'est un faux positif classique des outils souris globaux ; le code est entièrement auditable ici |
| Config cassée | Le logiciel garde l'ancienne config et affiche l'erreur ; validez avec `RavenneToolsMenu.exe --validate` |

---

## Feuille de route

- [x] Roue gestuelle + sous-menus + multi-niveaux
- [x] Roues contextuelles (application, fichiers, texte)
- [x] Conversions d'images, redimensionnement, ZIP, corbeille, ffmpeg
- [x] Mises à jour automatiques (GitHub Releases) + CI/Release GitHub Actions
- [ ] Éditeur graphique de roues (v1.1)
- [ ] Thèmes clair/personnalisés (v1.1)
- [ ] Raccourcis texte dynamiques (snippets) (v1.2)

*Projet personnel — code entièrement ouvert dans ce dépôt.*
