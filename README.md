# Zoo Logic

Jeu de puzzle logique type "Queens" pour Android (Unity/C#). Le joueur place un pion par zone colorée, par ligne et par colonne, sans jamais toucher en diagonale adjacent. Niveaux générés à la volée, progression infinie.

## Architecture

```
Assets/
├── Scripts/
│   ├── Core/           # Logique pure du puzzle (pas de Unity UI)
│   │   ├── PuzzleGrid.cs          – Modèle de données grille (zones, pions)
│   │   ├── RuleValidator.cs       – Vérification des 4 règles + détection conflits
│   │   ├── PuzzleSolver.cs        – Solveur backtracking (solution unique garantie)
│   │   ├── LevelGenerator.cs      – Génération procédurale de niveau
│   │   ├── LevelConfig.cs         – Formules taille/difficulté par numéro de niveau
│   │   └── DifficultyScorer.cs    – Estimation de la difficulté d'une configuration
│   │
│   ├── Game/           # UI procédurale, contrôleur, vues
│   │   ├── PuzzleGameController.cs – Contrôleur principal (taps, conflits, victoire/défaite)
│   │   ├── GridView.cs            – Affichage grille UI (cases, pions, highlights)
│   │   ├── CellView.cs            – Composant par case (animations pop/X/flash)
│   │   ├── GameHUD.cs             – HUD complet (header, règles, vies, score, indices)
│   │   ├── MainMenuBuilder.cs     – Menu principal 100% procédural
│   │   ├── LevelMapBuilder.cs     – Carte des niveaux (grille 4 colonnes, scroll infini)
│   │   ├── SettingsPanel.cs       – Panneau settings (SFX, haptics, reset progression)
│   │   ├── LevelProgressManager.cs – Persistance PlayerPrefs (étoiles, niveau débloqué)
│   │   ├── LivesManager.cs        – Système de 3 vies
│   │   ├── TutorialManager.cs     – Tutoriel 4 étapes
│   │   ├── AnimalIconSet.cs       – 30 sprites animaux pour les zones
│   │   ├── SFXManager.cs          – Système audio procédural
│   │   └── FeedbackUtils.cs       – Haptics Android
│   │
│   └── Editor/         # Outils de développement
│       ├── BuildAPK.cs            – Build Android automatique (IL2CPP/ARM64)
│       ├── TestGridSceneBuilder.cs – Constructeur scène de test
│       ├── TutorialSceneBuilder.cs – Constructeur scène tutoriel
│       ├── LevelMapSceneBuilder.cs – Constructeur scène carte niveaux
│       └── MainMenuSceneBuilder.cs – Constructeur scène menu
│
├── Scenes/
│   ├── MainMenu.unity   (index 0)
│   ├── LevelMap.unity   (index 1)
│   └── TestGrid.unity   (index 2)
│
├── Resources/
│   ├── Art/Animals/     – 30 sprites animaux (png)
│   ├── Fonts/Fredoka/   – Police Fredoka Bold/Regular SDF
│   ├── Sounds/          – 10 effets sonores (WAV)
│   └── UI/              – heart, star, potion, coin, X, gems (png)
│
└── EditorBuildSettings.asset  – Ordre des scènes
```

## Flux des scènes

```
MainMenu → LevelMap → TestGrid → (victoire) → TestGrid (niveau suivant)
               ↑                         ↓
               └───── (bouton retour) ───┘
```

- **"Continuer"** après victoire → charge directement `SelectedLevel + 1` dans la scène de jeu (pas de retour à LevelMap).
- **Bouton retour** (←) dans le HUD → retourne toujours à LevelMap.
- **Bouton retour** (←) sur LevelMap → retourne à MainMenu.
- **Android back** sur MainMenu → dialog "Quitter l'application ?".

## Décisions techniques importantes

### Active Input Handling
**DOIT rester sur "Input Manager (Old)"** dans Player Settings.
Les valeurs "New" ou "Both" causent des `NullReferenceException` sur Android car tout le code utilise l'API `Input.*` classique.

### Scripting Backend & Architecture
- **IL2CPP** avec **ARM64** activé (obligatoire pour les téléphones récents)
- **Mono2x** ne supporte pas ARM64 dans Unity 6000.x (case grise dans Player Settings)
- Le script `BuildAPK.cs` force `Active Input Handling = 0` (Old) via SerializedObject au build

### Android Gradle
- **AGP (Android Gradle Plugin)** : version 9.0.0 (par défaut dans Unity 6000.3.21f1)
- **Miroirs Aliyun** ajoutés dans `settingsTemplate.gradle` car `dl.google.com` est inaccessible depuis certains réseaux
- Min SDK: 29, Target SDK: 35

### Génération de niveaux
- **À la volée** (pas de banque pré-générée) via `LevelGenerator`
- Taille grille selon numéro : 4×4 (niv.1-3) → 5×5 (niv.4-15) → 6×6 (niv.16-40) → 7×7 (niv.41-80) → 8×8 (niv.81+)
- Difficulté cyclique : multiples de 5 → 1, multiples de 3 → 3, sinon → 2
- Solution unique garantie par `PuzzleSolver` (backtracking)

### UI 100% procédurale
- Zéro prefabs, zéro configuration Inspector
- Toute l'UI (menus, HUD, grilles, popups) est construite en code au runtime
- Sprites procéduraux pour formes arrondies, étoiles, engrenage, flèches, cadenas
- Police Fredoka chargée via `Resources.Load`

## Progrès récents

### Game feel (animations & feedback) — terminé
- **Kit de feedback réutilisable** (`FeedbackUtils.cs`) : courbes d'easing (OutBack, OutCubic, OutQuad, InQuad, OutElastic, InOutQuad), `Punch` (échelle en surtension, flash alpha), `ScreenShake` (tremblement amorti), haptics Android
- **ConfettiHelper.cs** : pluie de confettis procéduraux colorés (60-70 particules) à la victoire — zéro package, texture 1×1 teintée
- **SceneFader.cs** : fondu plein écran réutilisable (fade in à l'arrivée, fade out avant changement de niveau)
- **Grille** : apparition des cellules **en vague** (staggered, easeOutBack, décalées en diagonale) au chargement ; shake du plateau au conflit ; punch au placement valide
- **Cellules** : squish au toucher (échelle 0.9 → retour élastique), shrink-out du pion retiré, pop 3D de la marque X, conflit rouge + texte
- **Score** : punch + flash rouge de la pilule quand il baisse
- **Indice** : pulsation dorée avec variation d'échelle (±6 %)
- **SFX** : variation de pitch par effet (évite la monotonie)

### HUD / écrans — améliorations visuelles (éditeur)
- **GameHUD.cs** — encoche/notch gérée via `TopInset` (`Screen.safeArea` mobile, 70px simulée dans l'éditeur pour prévisualiser) ; layout recalculé en distances depuis le haut ; header agrandi (200px + encoche) ; pilules/score/indice/cœurs agrandis ; **ombres portées** sous pilules, header et cartes ; **palette plus vive** ; `BoardYOffset` recalculé dynamiquement
- **LevelMapBuilder.cs** — même gestion d'encoche (`CalcTopInset`) ; header agrandi (130px + encoche) avec ombre portée ; bouton retour avec ombre ; bulles/niveaux plus grandes (numéros 42→48-56, étoiles 28→34, cadenas 32→40) ; ombres des bulles renforcées ; bandeaux séparateurs bleu vif
- **SettingsPanel.cs** — panneau agrandi (850×700→900×760) avec **coins arrondis** + **vraie ombre portée** (frère décalé, remplace le `Shadow` built-in) ; titre/labels/toggles/boutons agrandis ; couleurs plus vives ; dialog de confirmation agrandi et arrondi

## TODO / Prochaines étapes

- [ ] Vérification visuelle finale des améliorations (HUD, Levels, Settings) dans l'éditeur
- [ ] Build APK + test sur appareil physique (confettis, shake, transitions, indice, score, HUD responsive)
- [ ] Icônes et splash screen Android à finaliser
- [ ] Intégration AdMob (rewarded + interstitial)
- [x] Renommage définitif du projet (Zoodoku → Zoo Logic)
- [ ] Sauvegarde cloud / Google Play Games
- [ ] Sons musicaux d'ambiance
- [ ] Accessibilité (taille de texte, contraste)

## Assets externes

| Pack | Licence | Utilisation |
|------|---------|-------------|
| **30 Animal Icons** (flaticon.com) | Flaticon Free License | Sprites de zones dans la grille + décorations menu |
| **Kenney UI Pack** (kenney.nl) | CC0 1.0 Universal | Sprites UI (heart, coin, potion, gems, star, X) |
| **Kenney Impact Sounds** | CC0 1.0 Universal | Effets sonores (Confirm, Failure, Success, etc.) |
| **Fredoka** (Google Fonts) | SIL Open Font License | Police principale (Bold + Regular) |
| **Kenney Future / Future Narrow** | SIL Open Font License | Police alternative (non utilisée actuellement) |

## Configuration de build

```bash
# Build via le menu Unity :
Tools > Zoo Logic > Build Android APK   # APK local (test / install directe)
Tools > Zoo Logic > Build Android AAB   # App Bundle (Google Play Store)

# Output : Builds/ZooLogic_v{version}.apk / .aab
# Cible : ARM64, IL2CPP, API 29-35, signé avec le keystore memorymatrix
```

**Signature (upload key)** : le build signe automatiquement avec
`Assets/play store/memorymatrix.keystore` (alias `memorymatrix`, mot de passe
dans `BuildAPK.cs`). Play Store rejette les AAB signés avec la clé debug — ne
pas retirer ce keystore.

**Icône & splash** : l'icône (`Assets/myicon.jpg`, 512×512) et le splash
Android (`Assets/Resources/UI/splash_android.png`, 1080×1920) sont appliqués
automatiquement au build (menu `Tools > Zoo Logic > Apply App Icon`).
