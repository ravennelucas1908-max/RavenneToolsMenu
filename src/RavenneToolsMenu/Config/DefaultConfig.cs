namespace RavenneToolsMenu.Config;

/// <summary>Configuration par défaut écrite au premier lancement (éditable ensuite).</summary>
internal static class DefaultConfig
{
    public const string Json = """
    {
      "version": 2,
      "trigger": {
        "button": "middle",
        "longPressMs": 320,
        "quickClickMs": 170,
        "quickClickPassthrough": true
      },
      "appearance": {
        "accent": "#7C4DFF",
        "radius": 150,
        "iconSize": 26,
        "animations": true,
        "showItemLabels": false,
        "opacity": 0.97
      },
      "ffmpeg": {
        "path": "ffmpeg"
      },
      "updates": {
        "repo": "ravennelucas1908-max/RavenneToolsMenu",
        "enabled": true,
        "skipVersion": ""
      },
      "skipFullscreen": true,
      "disabledProcesses": [ "mstsc", "vmconnect", "anydesk" ],

      "rules": [
        {
          "name": "Images sélectionnées",
          "match": { "selectedFiles": true, "fileExtensions": [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".ico", ".jfif"] },
          "menu": "images"
        },
        {
          "name": "Fichiers sélectionnés",
          "match": { "selectedFiles": true },
          "menu": "fichiers"
        },
        {
          "name": "Texte sélectionné",
          "match": { "selectedText": true },
          "menu": "texte"
        },
        {
          "name": "Navigateur",
          "match": { "processes": ["chrome", "msedge", "firefox", "brave", "opera", "vivaldi"] },
          "menu": "navigateur"
        }
      ],

      "menus": {
        "principal": {
          "label": "Ravenne's Tools",
          "items": [
            { "label": "Navigateur", "icon": "globe", "action": { "kind": "url", "url": "https://www.google.com" } },
            { "label": "Terminal", "icon": "terminal", "action": { "kind": "launch", "path": "wt.exe" }, "description": "Windows Terminal (repli : cmd)" },
            { "label": "Bloc-notes", "icon": "notepad", "action": { "kind": "launch", "path": "notepad.exe" } },
            {
              "label": "Média", "icon": "music",
              "items": [
                { "label": "Lecture / Pause", "icon": "play", "action": { "kind": "media", "media": "playpause" } },
                { "label": "Piste suivante", "icon": "next", "action": { "kind": "media", "media": "next" } },
                { "label": "Piste précédente", "icon": "prev", "action": { "kind": "media", "media": "prev" } },
                { "label": "Volume +", "icon": "volup", "action": { "kind": "media", "media": "volup" } },
                { "label": "Volume −", "icon": "voldn", "action": { "kind": "media", "media": "voldn" } },
                { "label": "Couper le son", "icon": "mute", "action": { "kind": "media", "media": "mute" } }
              ]
            },
            {
              "label": "Presse-papiers", "icon": "clipboard",
              "items": [
                { "label": "Copier", "icon": "copy", "action": { "kind": "keys", "keys": "ctrl+c" } },
                { "label": "Couper", "icon": "cut", "action": { "kind": "keys", "keys": "ctrl+x" } },
                { "label": "Coller", "icon": "paste", "action": { "kind": "keys", "keys": "ctrl+v" } },
                { "label": "Coller sans formatage", "icon": "paste", "action": { "kind": "keys", "keys": "ctrl+shift+v" } },
                { "label": "Vider", "icon": "trash", "action": { "kind": "system", "system": "clipboardClear" } }
              ]
            },
            {
              "label": "Système", "icon": "power",
              "items": [
                { "label": "Verrouiller", "icon": "lock", "action": { "kind": "system", "system": "lock" } },
                { "label": "Gestionnaire des tâches", "icon": "chart", "action": { "kind": "launch", "path": "taskmgr.exe" } },
                { "label": "Capture d'écran", "icon": "camera", "action": { "kind": "system", "system": "screenshot" }, "description": "Win+Maj+S" },
                { "label": "Montrer le bureau", "icon": "monitor", "action": { "kind": "system", "system": "showdesktop" } },
                { "label": "Vider la corbeille", "icon": "trash", "action": { "kind": "system", "system": "emptytrash" } },
                { "label": "Paramètres Windows", "icon": "settings", "action": { "kind": "launch", "path": "ms-settings:" } },
                { "label": "Veille", "icon": "moon", "action": { "kind": "system", "system": "sleep" } }
              ]
            },
            {
              "label": "Outils", "icon": "wrench",
              "items": [
                { "label": "Mon IP publique", "icon": "network", "action": { "kind": "script", "script": "(Invoke-WebRequest -Uri 'https://api.ipify.org' -UseBasicParsing).Content", "toastResult": true } },
                { "label": "Vider le cache DNS", "icon": "refresh", "action": { "kind": "script", "script": "ipconfig /flushdns", "toastResult": true } },
                { "label": "Dossier de configuration", "icon": "folder", "action": { "kind": "openConfigFolder" } },
                { "label": "Recharger la configuration", "icon": "refresh", "action": { "kind": "reloadConfig" } },
                { "label": "À propos", "icon": "info", "toast": "Ravenne's Tools Menu v1.0 — roue d'outils contextuelle." }
              ]
            }
          ]
        },

        "navigateur": {
          "label": "Navigateur",
          "items": [
            { "label": "Nouvel onglet", "icon": "copy", "action": { "kind": "keys", "keys": "ctrl+t" } },
            { "label": "Rouvrir l'onglet fermé", "icon": "refresh", "action": { "kind": "keys", "keys": "ctrl+shift+t" } },
            { "label": "Nouvelle fenêtre", "icon": "export", "action": { "kind": "keys", "keys": "ctrl+n" } },
            { "label": "Fenêtre privée", "icon": "lock", "action": { "kind": "keys", "keys": "ctrl+shift+n" }, "description": "Firefox : Ctrl+Maj+P" },
            { "label": "Téléchargements", "icon": "download", "action": { "kind": "keys", "keys": "ctrl+j" } },
            { "label": "Historique", "icon": "clock", "action": { "kind": "keys", "keys": "ctrl+h" } },
            {
              "label": "Zoom", "icon": "search",
              "items": [
                { "label": "Agrandir", "icon": "volup", "action": { "kind": "keys", "keys": "ctrl+plus" } },
                { "label": "Réduire", "icon": "voldn", "action": { "kind": "keys", "keys": "ctrl+minus" } },
                { "label": "Réinitialiser", "icon": "refresh", "action": { "kind": "keys", "keys": "ctrl+0" } }
              ]
            }
          ]
        },

        "fichiers": {
          "label": "Fichiers",
          "items": [
            { "label": "Copier les chemins", "icon": "copy", "action": { "kind": "copyPath", "mode": "full" }, "description": "Chemins complets" },
            { "label": "Copier les noms", "icon": "files", "action": { "kind": "copyPath", "mode": "names" } },
            { "label": "ZIP", "icon": "zip", "action": { "kind": "zip" }, "description": "Compresser la sélection" },
            { "label": "Ouvrir avec…", "icon": "export", "action": { "kind": "openWith" } },
            {
              "label": "Convertir", "icon": "convert",
              "items": [
                { "label": "PNG", "icon": "image", "action": { "kind": "convertImage", "convertTo": "png" } },
                { "label": "JPG", "icon": "image", "action": { "kind": "convertImage", "convertTo": "jpg", "quality": 90 } },
                { "label": "BMP", "icon": "image", "action": { "kind": "convertImage", "convertTo": "bmp" } },
                { "label": "GIF", "icon": "image", "action": { "kind": "convertImage", "convertTo": "gif" } },
                { "label": "TIFF", "icon": "image", "action": { "kind": "convertImage", "convertTo": "tiff" } },
                { "label": "ICO", "icon": "image", "action": { "kind": "convertImage", "convertTo": "ico" } },
                { "label": "MP3 (audio)", "icon": "music", "action": { "kind": "ffmpeg", "convertTo": "mp3" }, "description": "Nécessite ffmpeg" },
                { "label": "MP4 (vidéo)", "icon": "video", "action": { "kind": "ffmpeg", "convertTo": "mp4" }, "description": "Nécessite ffmpeg" }
              ]
            },
            { "label": "Terminal ici", "icon": "terminal", "action": { "kind": "openTerminalHere" } },
            { "label": "Recycler", "icon": "trash", "action": { "kind": "recycle" }, "description": "Envoyer à la corbeille" }
          ]
        },

        "images": {
          "label": "Images",
          "items": [
            {
              "label": "Convertir", "icon": "convert",
              "items": [
                { "label": "PNG", "icon": "image", "action": { "kind": "convertImage", "convertTo": "png" } },
                { "label": "JPG (q92)", "icon": "image", "action": { "kind": "convertImage", "convertTo": "jpg", "quality": 92 } },
                { "label": "BMP", "icon": "image", "action": { "kind": "convertImage", "convertTo": "bmp" } },
                { "label": "GIF", "icon": "image", "action": { "kind": "convertImage", "convertTo": "gif" } },
                { "label": "TIFF", "icon": "image", "action": { "kind": "convertImage", "convertTo": "tiff" } },
                { "label": "ICO", "icon": "image", "action": { "kind": "convertImage", "convertTo": "ico" } }
              ]
            },
            {
              "label": "Redimensionner", "icon": "resize",
              "items": [
                { "label": "25 %", "icon": "resize", "action": { "kind": "resizeImage", "scale": 0.25 } },
                { "label": "50 %", "icon": "resize", "action": { "kind": "resizeImage", "scale": 0.5 } },
                { "label": "1024 px de large", "icon": "resize", "action": { "kind": "resizeImage", "maxWidth": 1024 } },
                { "label": "512 px de large", "icon": "resize", "action": { "kind": "resizeImage", "maxWidth": 512 } }
              ]
            },
            { "label": "Compresser (JPG 80)", "icon": "image", "action": { "kind": "convertImage", "convertTo": "jpg", "quality": 80 } },
            { "label": "Copier le chemin", "icon": "copy", "action": { "kind": "copyPath", "mode": "full" } },
            { "label": "ZIP", "icon": "zip", "action": { "kind": "zip" } },
            { "label": "Recycler", "icon": "trash", "action": { "kind": "recycle" } }
          ]
        },

        "texte": {
          "label": "Texte",
          "items": [
            { "label": "Copier", "icon": "copy", "action": { "kind": "keys", "keys": "ctrl+c" } },
            { "label": "Rechercher sur Google", "icon": "search", "action": { "kind": "url", "url": "https://www.google.com/search?q={text}" } },
            { "label": "Traduire", "icon": "translate", "action": { "kind": "url", "url": "https://translate.google.com/?sl=auto&tl=fr&text={text}&op=translate" } },
            { "label": "Wikipédia", "icon": "globe", "action": { "kind": "url", "url": "https://fr.wikipedia.org/w/index.php?search={text}" } },
            { "label": "MAJUSCULES", "icon": "caseUpper", "action": { "kind": "textTransform", "textTransform": "upper" }, "description": "Remplace la sélection" },
            { "label": "minuscules", "icon": "caseLower", "action": { "kind": "textTransform", "textTransform": "lower" } },
            { "label": "Capitaliser", "icon": "caseTitle", "action": { "kind": "textTransform", "textTransform": "title" } },
            { "label": "Coller sans formatage", "icon": "paste", "action": { "kind": "keys", "keys": "ctrl+shift+v" } }
          ]
        }
      }
    }
    """;
}
