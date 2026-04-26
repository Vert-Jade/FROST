# ❄️ FROST - Assistant Tactique pour le Comte Harebourg

![Version](https://img.shields.io/badge/version-1.0.2-blue) ![Release](https://img.shields.io/github/v/release/Vert-Jade/FROST)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey) ![Status](https://img.shields.io/badge/status-stable-green)

**FROST** est une application d'overlay conçue pour aider les joueurs de **Dofus** à lire et calculer instantanément la mécanique de **Confusion** contre **le Comte Harebourg**, sans perturber le gameplay.

👉 Pensé par des joueurs, pour des joueurs, FROST reste **rapide, précis, lisible et 100% manuel**.

---

## ✨ Nouveautés v1.0.2

- 🚀 **Démarrage plus fiable**
  FROST gère maintenant proprement le tout premier lancement, y compris quand Dofus n'est pas encore ouvert.

- 📘 **Notice obligatoire fiabilisée**
  Le parcours de notice ne peut plus laisser l'application dans un état visible mais bloqué côté interactions.

- 🪟 **Tracking de fenêtre consolidé**
  Les améliorations de la v1.0.1 sont conservées et stabilisées pour les cas de masquage, restauration et resize de Dofus.

- 🔄 **Mises à jour prêtes pour la suite**
  L'auto-update introduit en **v1.0.1** reste en place pour détecter automatiquement les prochaines releases GitHub.

---

## 🖼️ Aperçu

<img width="805" height="447" alt="FROST_GitHub" src="https://github.com/user-attachments/assets/05a38865-1b6c-4235-88fd-36e9e000a122" />

---

## 📥 Téléchargement

- **Dernière version prête à l'emploi :**  
  https://github.com/Vert-Jade/FROST/releases/latest

- **Code source :**

```powershell
git clone https://github.com/Vert-Jade/FROST.git
cd FROST
```

---

## 🛡️ Sécurité & Conformité

- ❌ Aucune lecture mémoire du jeu
- ❌ Aucune analyse d'écran
- ❌ Aucune automatisation

👉 FROST est une **surcouche visuelle**.  
Toutes les actions sont effectuées **manuellement par le joueur**.

> 🧠 FROST = l'équivalent d'un papier + crayon, directement sur ton écran.

### 🔐 Analyse VirusTotal

La dernière analyse publique disponible peut être consultée sur VirusTotal ici :

https://www.virustotal.com/gui/file/179fb57452206823f7f57f73306890409784047171f37f9a75f6307a81cc8484?nocache=1

À ce stade, un moteur de détection **IA** isolé peut signaler le fichier de manière prudente, sans consensus global de détection malveillante. Cela correspond au type de faux positif que l'on peut parfois voir sur une application récente, peu diffusée et compilée récemment.

---

## ✨ Fonctionnalités principales

- 🎯 **Calcul instantané**
  Détermine la case à jouer selon le seuil de vitalité et l'état du combat.

- 🧠 **Gestion avancée des mécaniques**
  Gère le **Gousset** et les téléportations symétriques pair / impair.

- ⚔️ **Gestion du Pandultimatum**
  Chaque coup au corps-à-corps fait tourner la cible de **90° vers la droite**, avec ajustement manuel du nombre de frappes.

- 🪟 **Overlay intelligent**
  L'overlay suit la fenêtre Dofus, s'adapte aux resizes et se masque automatiquement si besoin.

- 🖱️ **Overlay transparent**
  L'interface laisse jouer normalement, avec click-through dynamique hors du panneau.

- 🌍 **Multilingue**
  Disponible en **12 langues**.

- 👁️ **Accessibilité**
  Support des profils daltoniens :
  **Protanopie**, **Deutéranopie**, **Tritanopie**.

- 📘 **Notice intégrée**
  Onboarding, aide embarquée et progression conservée localement.

---

## 🚀 Installation & Lancement

### Option 1 - Installer la version prête à l'emploi

1. Télécharge la dernière release.
2. Lance l'installateur Windows `FROST_v1.0.2_setup.exe`.
3. Suis l'assistant de configuration.

💡 Les **mises à jour automatiques sont disponibles à partir de la v1.0.1** pour les prochaines versions.

### Option 2 - Cloner et lancer le projet source

Le dépôt contient maintenant tout le nécessaire pour préparer le projet sans bricolage :

- `requirements.txt` : dépendance source principale attendue par `setup.ps1`
- `setup.ps1` : vérifie le SDK .NET 8, restore et build le projet
- `setup.bat` : lance `setup.ps1` en double-clic

Depuis le dossier cloné :

```powershell
.\setup.bat
```

Ou en PowerShell :

```powershell
.\setup.ps1 -Configuration Release -Run
```

---

## 📖 Utilisation

### ⚔️ En combat

1. Sélectionne ton **seuil de vitalité** ou colle une ligne de log de combat valide.
2. Appuie sur **F2**.
3. Clique :
   - ton personnage
   - puis la cible
4. Ajuste si nécessaire :
   - le type de cible
   - le nombre de frappes au corps-à-corps

👉 FROST affiche immédiatement la case finale à jouer.

### 🪟 Comportement de l'overlay

- FROST suit automatiquement la fenêtre Dofus
- FROST se recale pendant les redimensionnements
- FROST se masque quand Dofus est réduit ou indisponible

---

## ⌨️ Raccourcis clavier

| Action | Touche | Description |
| --- | --- | --- |
| Lancer / Valider | `F2` | Démarre le ciblage |
| Afficher / Masquer | `F3` | Bascule la visibilité |
| Effacer | `F4` | Réinitialise l'état en cours |

> 💡 Astuce : le panneau reste compact et discret pour ne pas gêner le combat.

---

## 🔄 Mises à jour

À partir de la **v1.0.1** :

- FROST vérifie `releases/latest` sur GitHub au démarrage
- télécharge automatiquement l'installateur si une version plus récente existe
- propose ensuite de lancer la mise à jour

⚠️ Pour qu'une mise à jour soit détectée, la prochaine release doit avoir une **version strictement supérieure** à celle déjà installée.

---

## 🛠️ Build & Distribution

### Build manuel de l'application

```powershell
dotnet build FROST.sln -c Release
```

### Lancer l'application en local

```powershell
dotnet run --project FROST.csproj -c Release
```

### Générer l'installateur

```powershell
.\build-installer.ps1 -Configuration Release -Runtime win-x64
```

Le script :

1. publie l'application en self-contained
2. installe Inno Setup via `winget` si besoin
3. génère un installateur au format `FROST_vX.Y.Z_setup.exe`

---

## 📋 Prérequis source

- Windows 10 ou 11
- .NET 8 SDK
- `winget` si tu veux que les scripts installent automatiquement les dépendances manquantes

---

## 🛠️ Stack technique

- **Langage** : C#
- **Framework** : .NET 8 / WPF
- **Architecture** : Overlay Windows avec `user32.dll`
- **Distribution** : GitHub Releases + installateur Inno Setup

---

## 👥 Crédits

Projet développé avec passion pour la communauté **Dofus**.

- 👤 Luframe
- 👤 Vert-Jade

---

## ⚠️ Disclaimer

FROST n'est **pas affilié**, **ni soutenu**, **ni sponsorisé** par Ankama.

---

## ⭐ Support

Si le projet t'aide, n'hésite pas à :

- laisser une étoile sur le repo
- ouvrir une issue en cas de bug
- proposer des améliorations
