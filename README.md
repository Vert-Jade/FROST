# ❄️ FROST - Assistant Tactique pour le Comte Harebourg

![Version](https://img.shields.io/badge/version-1.0-blue)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)
![Status](https://img.shields.io/badge/status-stable-green)
![Release](https://img.shields.io/github/v/release/Vert-Jade/FROST)

---

**FROST** est une application d'overlay (superposition d'écran) conçue pour aider les joueurs de **Dofus** à comprendre et calculer instantanément la mécanique de **"Confusion"** lors du combat contre le boss **Comte Harebourg**.

👉 Conçu par des joueurs, pour les joueurs, FROST est **rapide, précis, personnalisable et 100% sécurisé**.

---

## 📥 Téléchargement

Deux options sont disponibles :

- **Version prête à l'emploi (setup.exe)** :  
  https://github.com/Vert-Jade/FROST/releases/latest
- **Code source du projet** :  
  `git clone https://github.com/Vert-Jade/FROST.git`

---

## 🛡️ Sécurité & Conformité

- ❌ Aucune lecture mémoire du jeu (pas d'injection)
- ❌ Aucune analyse d'écran (pas d'OCR / pixel scanning)
- ❌ Aucune automatisation (pas de macro / bot)

👉 L'application est une **simple surcouche visuelle (overlay)**.  
Toutes les actions sont effectuées **manuellement par le joueur**.

> 🧠 FROST = l'équivalent d'un papier + crayon, mais directement sur ton écran.

---

## ✨ Fonctionnalités principales

- 🎯 **Calcul instantané**  
  Détermine la case exacte en fonction du seuil de vitalité (Pi, Pi/2, etc.)

- 🧠 **Gestion avancée (Gousset)**  
  - Rotation des frappes au corps-à-corps (90° par coup)  
  - Téléportation symétrique selon la parité du tour (Pair / Impair)

- 🖱️ **Overlay transparent**  
  Interaction possible à travers l’interface (aucune gêne en jeu)

- 🌍 **Multilingue**  
  Disponible en 12 langues (FR, EN, ES, PT, DE, IT…)

- 👁️ **Accessibilité**  
  Support du daltonisme :
  - Protanopie  
  - Deutéranopie  
  - Tritanopie  

- 📏 **Calibration personnalisée**  
  Compatible avec toutes les résolutions et configurations d’écran

---

## 🚀 Installation & Lancement

### Option 1 - Installer la version prête à l'emploi

1. Télécharge la dernière version depuis :  
   https://github.com/Vert-Jade/FROST/releases

2. Lance : FROST_v1.0.0_Setup.exe

3. Suis l’assistant de configuration :
   - écran
   - langue
   - couleurs

### Option 2 - Cloner et lancer le projet depuis le code source

Prérequis :

- Windows
- .NET 8 SDK
- Visual Studio 2022 (recommandé) ou la CLI `dotnet`

1. Clone le dépôt :

```powershell
git clone https://github.com/Vert-Jade/FROST.git
cd FROST
```

2. Lance le projet :

- **Avec Visual Studio** : ouvre `FROST.sln`, puis démarre le projet
- **Avec la CLI** :

```powershell
dotnet run --project FROST.csproj -c Release
```

3. Pour générer les binaires sans lancer l'application :

```powershell
dotnet build FROST.sln -c Release
```

---

## 📖 Utilisation

### 🔧 1. Calibration (une seule fois)

Dans les paramètres :

- Clique sur **Activer le mode Calibration**

Contrôles :
- **Clic droit maintenu** → déplacer la grille
- **Ctrl + molette** → zoom
- **Shift + molette** → largeur des cases
- **Alt + molette** → hauteur des cases

---

### ⚔️ 2. En combat

1. Sélectionne ton **seuil de vitalité**
   - ou colle le message du chat Dofus  
     *(ex: `[16:12] Nom du joueur Confusion horaire`)*

2. Appuie sur **F2**

3. Clique :
   - ton personnage
   - puis la cible

👉 FROST affiche immédiatement la case de frappe optimale

---

## ⌨️ Raccourcis clavier

| Action | Touche | Description |
|------|------|------------|
| Lancer / Valider | `F2` | Démarre le ciblage |
| Afficher / Masquer | `F3` | Toggle visibilité |
| Effacer | `F4` | Reset complet |

> 💡 Astuce : double-clique sur la fenêtre pour activer le mode compact

---

## 🛠️ Stack technique

- **Langage** : C#  
- **Framework** : .NET 8.0 / WPF  
- **Architecture** : Overlay Windows (User32.dll) avec Click-Through dynamique  

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

Si le projet t’aide, n’hésite pas à :
- ⭐ laisser une étoile sur le repo
- 🐛 signaler un bug
- 💡 proposer des améliorations


---

## 🔐 Analyse de sécurité (VirusTotal)

Le fichier a été analysé via VirusTotal afin de garantir son intégrité et sa sécurité.

👉 Résultat de l'analyse :  
https://www.virustotal.com/gui/file/e1fe3640fb07fe502cd3532053ed865909a8c2e556af32cf5b3cc1f6e7f141d4

Aucune activité malveillante significative n'a été détectée.

> ⚠️ Note : certains antivirus peuvent générer des faux positifs sur des applications peu connues ou récemment compilées.  
FROST ne contient aucun code malveillant.
