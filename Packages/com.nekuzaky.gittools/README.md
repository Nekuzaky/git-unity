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

Le package fournit deux fenêtres, toutes deux sous le menu `Tools > Git`.

### `Tools > Git > Dashboard` (`Ctrl+Shift+G`)

La vue complète, organisée en trois zones redimensionnables.

| Zone | Contenu |
| --- | --- |
| Barre d'outils | Actualiser, Fetch, Pull, Push, création de branche, menu Stash, filtre de recherche, nombre de commits affichés |
| Sidebar | Branche courante et son suivi, modifications en cours, branches locales (avec compteurs ↑↓), branches distantes, tags, stashes |
| Graphe | Historique avec lanes colorées, nœuds pleins pour les commits et évidés pour les fusions, badges de branches et de tags, auteur, date relative, SHA |
| Détail | Fichiers du commit sélectionné, ou zone d'indexation et de commit quand la ligne « Modifications non commitées » est sélectionnée |
| Diff | Diff colorisé du fichier sélectionné, avec défilement virtualisé |
| Console git | Chaque commande exécutée et sa sortie brute |

Doubler-cliquer une branche bascule dessus. Un clic droit ouvre un menu contextuel :

- **sur une branche locale** — basculer, fusionner dans la branche courante, renommer, supprimer
  (avec proposition de forcer si elle n'est pas fusionnée) ;
- **sur une branche distante** — créer une branche locale de suivi, fusionner, supprimer sur le distant ;
- **sur un commit** — copier le SHA ou le message, créer une branche ou un tag ici, basculer dessus,
  cherry-pick, revert, `reset --mixed` ou `reset --hard` ;
- **sur un tag** — supprimer, pousser ;
- **sur un stash** — appliquer, appliquer et retirer (`pop`), supprimer.

Cliquer une branche dans la sidebar fait défiler le graphe jusqu'à son sommet.

### `Tools > Git > Panneau rapide`

Une fenêtre compacte, sans historique : indexation, commit, push/pull et branches.
Utile en panneau étroit ancré à côté de l'Inspector.

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
