# FROST - Assistant Tactique pour le Comte Harebourg

![Version](https://img.shields.io/badge/version-1.0.1-blue)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)
![Status](https://img.shields.io/badge/status-stable-green)
![Release](https://img.shields.io/github/v/release/Vert-Jade/FROST)

FROST est un overlay Windows pour Dofus qui aide a lire et calculer instantanement la mecanique de Confusion du Comte Harebourg.

L'application reste 100% manuelle et 100% visuelle :

- aucune lecture memoire
- aucune analyse d'ecran
- aucune automatisation

## Telechargement

- Derniere version prete a l'emploi : https://github.com/Vert-Jade/FROST/releases/latest
- Code source :

```powershell
git clone https://github.com/Vert-Jade/FROST.git
cd FROST
```

## Fonctionnalites

- Calcul instantane de la case a jouer selon le seuil de vitalite
- Gestion du Gousset avec teleports symetriques pair / impair
- Gestion du Pandultimatum : chaque coup au cac tourne la cible de 90 degres vers la droite
- Saisie manuelle du seuil ou collage direct d'une ligne de log de combat
- Overlay transparent qui suit la fenetre Dofus et se recale pendant les resizes
- Masquage automatique de FROST quand Dofus est reduit ou indisponible
- Panneau compact / complet, selection d'ecran, onboarding et notice integree
- Multi-langue et profils daltonisme
- Verification automatique des nouvelles releases GitHub au demarrage
- Telechargement automatique de l'installeur de mise a jour
- Configuration locale conservee entre les mises a jour

## Installation

### Option 1 - Setup Windows

1. Telecharge la derniere release GitHub.
2. Lance l'installateur Windows.
3. Suis l'assistant.
4. Les prochaines versions plus recentes pourront etre detectees et proposees automatiquement au demarrage.

### Option 2 - Source plug and play

Le depot contient les scripts utiles pour preparer le projet sans configuration manuelle :

- `requirements.txt` : dependance source principale attendue par `setup.ps1`
- `setup.ps1` : verifie le SDK .NET 8, le restore et le build
- `setup.bat` : lance `setup.ps1` en double-clic

Depuis le dossier clone :

```powershell
.\setup.bat
```

Ou en PowerShell :

```powershell
.\setup.ps1 -Configuration Release -Run
```

## Lancement manuel

Pour lancer l'application :

```powershell
dotnet run --project FROST.csproj -c Release
```

Pour compiler uniquement :

```powershell
dotnet build FROST.sln -c Release
```

## Build installateur

Un script de build Inno Setup est inclus :

```powershell
.\build-installer.ps1 -Configuration Release -Runtime win-x64
```

Le script :

1. publie l'application en self-contained
2. installe Inno Setup via `winget` si besoin
3. genere un installateur `FROST_vX.Y.Z_setup.exe`

## Utilisation rapide

1. Selectionne ton seuil de vitalite ou colle une ligne de log de combat.
2. Appuie sur `F2`.
3. Clique ton personnage puis la cible.
4. Ajuste si besoin le type de cible et le nombre de frappes au cac.

FROST affiche immediatement la case finale a jouer.

## Raccourcis

| Action | Touche |
| --- | --- |
| Lancer / Valider | `F2` |
| Afficher / Masquer | `F3` |
| Effacer | `F4` |

## Mises a jour

- FROST verifie `releases/latest` sur GitHub au demarrage.
- Si une version strictement plus recente existe, l'installateur est telecharge puis propose a l'utilisateur.
- Pour forcer une vraie mise a jour chez les utilisateurs, il faut publier une nouvelle version plus haute que l'actuelle.

## Prerequis source

- Windows 10 ou 11
- .NET 8 SDK
- `winget` si tu veux que les scripts installent automatiquement les dependances manquantes

## Stack technique

- C#
- .NET 8
- WPF
- Overlay Windows avec `user32.dll`
- GitHub Releases pour la distribution et les mises a jour

## Credits

Projet developpe pour la communaute Dofus.

- Luframe
- Vert-Jade

## Disclaimer

FROST n'est ni affilie, ni soutenu, ni sponsorise par Ankama.

## Support

Si le projet t'aide :

- laisse une etoile sur le repo
- ouvre une issue en cas de bug
- propose des ameliorations
