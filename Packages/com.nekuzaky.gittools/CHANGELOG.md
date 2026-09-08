# Changelog

Toutes les modifications notables de ce package sont documentees ici.
Le format suit [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/)
et le versionnage suit [Semantic Versioning](https://semver.org/lang/fr/).

## [0.1.0] - 2026-09-08

### Ajoute

- Fenetre `Tools > Git` (raccourci `Ctrl+Shift+G`).
- Indexation et desindexation par fichier ou en bloc, annulation des modifications.
- Commit avec option `Amend`, et `Commit & Push` en une action.
- Fetch, Pull, Push avec creation automatique de l'upstream au premier push.
- Bascule de branche, creation de branche, fusion `--no-ff`.
- Detection des conflits, appel de `git mergetool` et abandon de fusion.
- Affichage du diff du fichier selectionne, cote index ou copie de travail.
- Console affichant chaque commande git et sa sortie brute.
- Rafraichissement automatique du statut toutes les 5 secondes, desactivable.
