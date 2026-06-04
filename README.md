# ❄️ FROST - Assistant Tactique pour le Comte Harebourg

![Version](https://img.shields.io/badge/version-1.0.9-blue) ![Release](https://img.shields.io/github/v/release/Vert-Jade/FROST)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey) ![Status](https://img.shields.io/badge/status-stable-green)

**FROST** est un overlay tactique pour **Dofus** pensé pour lire et calculer instantanément la mécanique de **Confusion** du **Comte Harebourg**, sans automatisation et sans perturber le gameplay.

👉 Pensé par des joueurs, pour des joueurs, FROST reste **rapide, lisible, précis et 100% manuel**.

---

## ✨ Nouveautés v1.0.9

- 🔄 **Mise à jour vraiment robuste**
  L'installateur ferme l'instance FROST installée, attend que Windows libère `FROST.exe`, puis remplace l'exécutable via un fichier temporaire pour éviter l'erreur de processus déjà utilisé.

- 💾 **Paramètres conservés à chaque mise à jour**
  Les sauvegardes, couleurs, profils, logs, mises à jour téléchargées et la Notice validée restent dans `%LOCALAPPDATA%\FROST` et ne sont pas supprimés par l'installateur.

- 🧊 **Fermeture en arrière-plan optionnelle**
  Les paramètres proposent maintenant de réduire FROST dans les icônes cachées au lieu de quitter totalement. Double-clic sur l'icône FROST pour restaurer, ou menu de l'icône pour quitter.

- 🧼 **Installateur simplifié et plus fiable**
  Les options d'épinglage au menu Démarrer et à la barre des tâches ont été retirées pour éviter les comportements Windows incohérents selon les sessions utilisateur.

- 🧭 **Premier lancement verrouillé au premier plan**
  L'assistant de départ et la notice obligatoire restent visibles devant Dofus, même si le jeu est déjà ouvert.

- 📐 **Hauteur initiale stabilisée**
  Le panneau recalcule sa hauteur au retour de l'écran d'attente Dofus pour éviter le vide fantôme en bas de l'UI.

- 🪟 **Prompt de mise à jour accessible**
  Une mise à jour peut être proposée même lorsque Dofus est fermé et que FROST affiche l'écran d'attente, sans rouvrir l'UI principale hors jeu.

- 🧹 **Désinstallation complète par défaut**
  Le désinstallateur supprime aussi le dossier interne FROST par défaut, avec l'option de conserver les données si besoin.

- 🧩 **Démarrage initial plus propre**
  Le setup de départ s'affiche directement, sans flash du panneau principal avant l'assistant.

- 🧹 **Désinstallateur FROST intégré**
  Windows lance maintenant un vrai désinstallateur visuel via `FROST.exe --uninstall`, sans script `.ps1`, avec conservation optionnelle des réglages.

- 📁 **Accès direct au dossier interne**
  Les paramètres proposent un bouton pour ouvrir le dossier `%LOCALAPPDATA%\FROST`, où se trouvent les sauvegardes, logs, mises à jour téléchargées et le raccourci de désinstallation.

- 🧊 **Installateur modernisé**
  Le setup Windows adopte un visuel FROST dédié, une identité plus propre et une sortie de build prête à publier.

- 🌍 **Assistant d'installation en 12 langues**
  L'installateur propose maintenant les mêmes langues que l'application :
  **français**, **anglais**, **espagnol**, **allemand**, **italien**, **néerlandais**, **polonais**, **portugais**, **russe**, **suédois**, **turc** et **arabe**.

- 📦 **Distribution 1.0.9 prête pour GitHub Releases**
  Le script de build génère l'installateur WPF moderne dans `release\modern-setup` et copie `FROST_v1.0.9_setup.exe` directement dans `release`.

- 🎯 **Ordre de sélection configurable**
  FROST permet maintenant de choisir entre **Joueur -> Cible** et **Cible -> Joueur**. Le mode par défaut passe en **Cible -> Joueur**, et la légende ainsi que les calculs suivent automatiquement cet ordre.

- 🎮 **Focus jeu préservé pendant la séquence**
  Le ciblage rend proprement la main à **Dofus** pour éviter de devoir recliquer la fenêtre avant de rejouer ses sorts et raccourcis.

- 🪟 **Overlay mieux attaché à Dofus**
  Le suivi de fenêtre a été consolidé pour garder FROST lié à la page Dofus suivie, sans détachement visuel intempestif.

- 🎨 **Couleurs de légende personnalisables**
  Chaque case colorée peut maintenant être personnalisée avec un sélecteur intégré à l'UI FROST et un code HEX. L'affichage suit les changements dans les modes **Joueur -> Cible**, **Cible -> Joueur** et les profils **daltoniens**.

- 🔄 **Mises à jour plus visibles**
  En plus de la vérification automatique au démarrage, un bouton permet désormais de **rechercher une mise à jour** directement depuis les paramètres.

- 📐 **UI plus propre au resize**
  Les seuils et certains blocs de paramètres ont été rééquilibrés pour éviter les écrasements visuels quand la fenêtre est redimensionnée.

---

## 🖼️ Aperçu

<img width="1920" height="1080" alt="FROST" src="https://github.com/user-attachments/assets/018fadb6-6e16-4e7c-9334-6e967bfd42f4" />

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

Quand une analyse publique récente est disponible, elle peut être partagée depuis la page de release correspondante.

À ce stade, un moteur de détection **IA** isolé peut parfois signaler le fichier de manière prudente, sans consensus global de détection malveillante. Cela correspond au type de faux positif que l'on peut voir sur une application récente, peu diffusée et compilée récemment.

---

## ✨ Fonctionnalités principales

- 🎯 **Calcul instantané**
  Détermine la case à jouer selon le seuil de vitalité et l'état du combat.

- 🔀 **Ordre de sélection configurable**
  Permet de cibler soit **le joueur puis la cible**, soit **la cible puis le joueur** selon tes habitudes.

- 🧠 **Gestion avancée des mécaniques**
  Gère le **Gousset** et les téléportations symétriques pair / impair.

- ⚔️ **Gestion du Pandultimatum**
  Chaque coup au corps-à-corps fait tourner la cible de **90° vers la droite**, avec ajustement manuel du nombre de frappes.

- 🪟 **Overlay intelligent**
  L'overlay suit la fenêtre Dofus, reste attaché au client suivi et se masque automatiquement si besoin.

- 🖱️ **Overlay transparent**
  L'interface laisse jouer normalement, avec click-through dynamique hors du panneau.

- 🎨 **Couleurs personnalisées**
  Les couleurs de légende et de repères peuvent être adaptées avec un sélecteur intégré et un code HEX.

- 👁️ **Accessibilité**
  Support des profils daltoniens :
  **Protanopie**, **Deutéranopie**, **Tritanopie**.

- 🌍 **Multilingue**
  Disponible en **12 langues**.

- 📘 **Notice intégrée**
  Onboarding, aide embarquée et progression conservée localement.

---

## 🚀 Installation & Lancement

### Option 1 - Installer la version prête à l'emploi

1. Télécharge la dernière release.
2. Lance l'installateur Windows `FROST_v1.0.9_setup.exe`.
3. Suis l'assistant de configuration.

💡 FROST vérifie automatiquement les nouvelles releases au démarrage et peut aussi les rechercher manuellement depuis les paramètres.

### Option 2 - Cloner et lancer le projet source

Le dépôt contient tout le nécessaire pour préparer le projet sans bricolage :

- `requirements.txt` : dépendances source attendues par `setup.ps1`
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
3. Clique par défaut :
   - la cible
   - puis ton personnage
4. Si besoin, inverse cet ordre dans les paramètres avec le mode **Joueur -> Cible**.
5. Ajuste ensuite :
   - le type de cible
   - le nombre de frappes au corps-à-corps

👉 FROST affiche immédiatement la case finale à jouer.

### 🎨 Personnalisation visuelle

- Clique sur une **case colorée** dans la légende des paramètres.
- Saisis un **code HEX** ou utilise la **palette intégrée**.
- Valide pour enregistrer, ou réinitialise pour revenir à la couleur cohérente du mode actif.

### 🪟 Comportement de l'overlay

- FROST suit automatiquement la fenêtre Dofus
- FROST se recale pendant les redimensionnements
- FROST se masque quand Dofus est réduit ou indisponible
- FROST relâche le focus vers le jeu pendant la séquence de ciblage
- FROST peut rester actif dans les icônes cachées au lieu de quitter quand l'option dédiée est activée

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

- FROST vérifie `releases/latest` sur GitHub au démarrage
- télécharge automatiquement l'installateur si une version plus récente existe
- propose ensuite d'installer la mise à jour
- permet aussi une **recherche manuelle** via le bouton dédié en bas des paramètres

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
2. compile l'installateur WPF moderne en self-contained
3. copie `FROST_v1.0.9_setup.exe` dans `release`

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
- **Distribution** : GitHub Releases + installateur WPF moderne

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

---

## 🛡️ Vérification sécurité

Analyse VirusTotal publiée pour le setup officiel `FROST_v1.0.9_setup.exe` :

- SHA256 : `03969B2718BDC55A171BD45789A61428609B4DE40E8FFB3BA74C402A741328A2`
- [Consulter l'analyse VirusTotal du setup v1.0.9](https://www.virustotal.com/gui/file-analysis/MmRkY2JiMTYyNjkxYWJhNjNhZjQzNzYxNTIzOGZiMWM6MTc4MDU4NjYyMw==)
