# Git Tools

Client Git intégré à l'éditeur Unity. Indexer, committer, pousser, tirer, changer de branche
et fusionner sans jamais quitter le moteur.

Le package appelle l'exécutable `git` installé sur la machine : pas de DLL native embarquée,
Git LFS et le gestionnaire d'identifiants du système continuent de fonctionner tels quels.

## Installation

### Par URL git (Package Manager)

`Window > Package Manager > + > Install package from git URL…` puis :

```
https://github.com/Nekuzaky/git-unity.git?path=/Packages/com.nekuzaky.gittools
```

Pour épingler une version, ajoute une révision :

```
https://github.com/Nekuzaky/git-unity.git?path=/Packages/com.nekuzaky.gittools#v0.1.0
```

### Par `manifest.json`

```json
{
  "dependencies": {
    "com.nekuzaky.gittools": "https://github.com/Nekuzaky/git-unity.git?path=/Packages/com.nekuzaky.gittools"
  }
}
```

### En local

Copie le dossier dans `Packages/` du projet, ou ajoute-le via
`Package Manager > + > Install package from disk…` en pointant sur `package.json`.

## Prérequis

- Unity 2021.3 ou plus récent.
- `git` 2.23+ accessible dans le `PATH` (la commande `git restore` est utilisée).
- Un dépôt Git initialisé à la racine du projet (le dossier qui contient `Assets/`).

## Utilisation

`Tools > Git`, ou `Ctrl+Shift+G` (`Cmd+Shift+G` sur macOS).

| Zone | Contenu |
| --- | --- |
| Barre d'outils | Actualiser, Fetch, Pull, Push, bascule de rafraîchissement automatique (5 s) |
| Barre de branche | Branche courante, upstream, nombre de commits à pousser / à tirer |
| Indexé | Fichiers prêts à être commités — `−` pour désindexer |
| Modifications | Fichiers modifiés et non suivis — `+` pour indexer, `x` pour annuler |
| Diff | Différences du fichier sélectionné, côté index ou côté copie de travail |
| Commit | Message multi-lignes, option `Amend`, `Commit` et `Commit & Push` |
| Branches | Bascule, création, fusion (`--no-ff`) |
| Console git | Chaque commande exécutée et sa sortie brute |

Sélectionner un fichier dans la liste le met aussi en surbrillance dans la fenêtre Project
lorsqu'il se trouve sous `Assets/`.

Toute opération qui modifie le disque (pull, checkout, merge, annulation) déclenche
un `AssetDatabase.Refresh()`.

## Conflits sur les scènes et les prefabs

Le bandeau de conflit propose un bouton **Résoudre (UnityYAMLMerge)** qui appelle
`git mergetool`. Il faut avoir déclaré l'outil une fois dans le dépôt :

```bash
git config merge.tool unityyamlmerge
git config mergetool.unityyamlmerge.trustExitCode false
git config mergetool.unityyamlmerge.keepBackup false
git config mergetool.unityyamlmerge.cmd '"<Unity>/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
```

Remplace `<Unity>` par le chemin de ton installation, par exemple
`C:/Program Files/Unity/Hub/Editor/6000.3.13f1`.

Vérifie également dans `Project Settings > Editor` que **Asset Serialization** est sur
**Force Text** : sans cela, les scènes sont binaires et aucun outil ne peut les fusionner.

## Notes de conception

- Les commandes tournent sur un thread de fond ; le callback est renvoyé sur le thread
  principal de l'éditeur, qui ne se fige donc jamais. Délai maximal : 120 s par commande.
- `GIT_TERMINAL_PROMPT=0` est forcé : une commande qui réclamerait des identifiants
  échoue proprement dans la console au lieu de bloquer sur une invite invisible.
- Le message de commit passe par un fichier temporaire (`git commit --file=`), ce qui
  élimine les problèmes de guillemets, d'accents et de retours à la ligne.
- Les actions destructrices (annuler un fichier, tout annuler, changer de branche avec des
  modifications en cours, fusionner) demandent confirmation.

## Limites connues

- Pas encore d'historique des commits, de stash, ni de sélection multiple par cases à cocher.
- Le staging partiel (par ligne ou par bloc) n'est pas géré : l'indexation se fait par fichier.
- Les dépôts avec sous-modules ne sont pas gérés spécifiquement.

## Licence

MIT — voir [LICENSE.md](LICENSE.md).
