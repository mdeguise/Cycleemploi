# Revue de l'application — Modifications appliquées

**Document source :** `2026-08-10 - Revue colleague - Formulaire integration.docx`
(captures d'écran annotées par une collègue, revue de l'application Cycle Emploi)

**Date de traitement :** 2026-08-10
**Commits :** [`75da91c`](https://github.com/mdeguise/Cycleemploi/commit/75da91c) (revue, sauf format de date), [`29bbc14`](https://github.com/mdeguise/Cycleemploi/commit/29bbc14) (format de date)

---

## Étape 1 — Employé

| Commentaire | Statut |
|---|---|
| Reformuler la description de « Nouvelle intégration » | ✅ Appliqué |
| Ajouter une description à « Avis de terminaison ou mise à pied temporaire » | ✅ Appliqué |
| Rendre le format de date plus visuel/français (JJ-MM-AAAA) | ✅ Appliqué — nouveau composant `DateInput` (calendrier personnalisé), voir section « Format de date » ci-dessous |
| Reformuler l'avis « employés actifs » (mise à pied) | ✅ Reformulation seulement — **le comportement réel n'a pas changé** : un employé en mise à pied est toujours considéré actif et apparaît dans la recherche. Confirmé explicitement avant modification, puisque le libellé proposé par la collègue inversait accidentellement cette logique. |

## Étape 1 — Règle de paye

| Commentaire | Statut |
|---|---|
| Affichage plus visuel (icônes horloge + repas) | ✅ Appliqué — nouveau composant `RegleDePayeSelect` remplaçant le `<select>` simple par un menu déroulant avec icônes, ex. « 🕐 7 h 30 \| 🍽 60 min » |

## Étape 3 — Accès et comptes

| Commentaire | Statut |
|---|---|
| Reformuler le sous-titre | ✅ Appliqué |
| Descriptions étendues pour chaque carte (AD/courriel, VPN, Badge, Code d'alarme) | ✅ Appliqué |
| Sous-titre pour la section « Système POS et Hébergement » | ✅ Appliqué |
| Descriptions pour RTP, SMS, OPERA, SYMPHONIE, APROPOS | ✅ Appliqué — RTP marqué « Facultatif »; SYMPHONIE inclut la mention « (Simphony) » |
| Texte d'aide pour « Stationnement requis » | ✅ Appliqué |
| Nouvel encadré « Important » (ne sélectionner que les accès nécessaires) | ✅ Appliqué |

## Étape 4 — Équipement

| Commentaire | Statut |
|---|---|
| Reformuler le sous-titre | ✅ Appliqué |
| Description pour la catégorie Télécommunications | ✅ Appliqué |
| « Téléphone cellulaire » → description « Téléphone mobile » | ✅ Appliqué (description ajoutée, le nom de la carte n'a pas été renommé) |
| « Radio bidirectionnelle » → description « Radio portative » | ✅ Appliqué (idem) |
| Renommer « Notes sur l'équipement » → « Précision sur l'équipement demandé » | ✅ Appliqué, avec placeholder détaillé |
| Nouvel encadré « Important » (rappel sur le processus d'approbation) | ⏭️ **Non ajouté** — le contenu proposé chevauchait presque entièrement l'encadré rose déjà existant sur cette étape. Décision : garder l'encadré existant plutôt que d'ajouter une deuxième boîte redondante. À revoir si une distinction était voulue. |
| Afficher « date d'entrée en fonction » dans le résumé | ✅ Appliqué — nouvelle ligne dans le résumé (barre latérale), avec équivalent « Dernière journée » côté cessation |

## Étape 5 — Applications

| Commentaire | Statut |
|---|---|
| Reformuler le sous-titre | ✅ Appliqué |
| Retirer la ligne « Microsoft » sous chaque appli, la remplacer par une description fonctionnelle | ✅ Appliqué |
| En-tête « Autres applications requises » | ✅ Appliqué |
| Reformuler l'instruction du champ libre | ✅ Appliqué |
| Exemples dans le placeholder (Foxit, Visio, Project, Power BI) | ✅ Appliqué |
| Note sur l'approbation des licences | ✅ Appliqué |

## Format de date (analyse séparée demandée)

Analyse effectuée avant modification pour vérifier l'impact sur la logique :

- Les dates sont stockées en format ISO (`AAAA-MM-JJ`) de bout en bout — état de l'application, base de données (`DateOnly`) — et ne sont converties en français que pour l'affichage (`formatDateFr`), déjà présent avant cette revue.
- **Aucun risque de logique** : le format d'affichage est totalement découplé du stockage/de la validation.
- Seule exception : les champs `<input type="date">` natifs, dont l'affichage est contrôlé par le navigateur/système d'exploitation, pas par le code de l'application.
- **Solution retenue** : nouveau composant `DateInput` (calendrier personnalisé, jours de la semaine en français, lundi en premier) remplaçant les deux champs natifs (« Date d'entrée prévue » à l'étape 1, « Dernière journée » à l'étape 2 de cessation). Affiche et permet la saisie en `JJ-MM-AAAA`; la valeur stockée reste `AAAA-MM-JJ`, aucun changement au backend ou aux types de données.

## Décisions nécessitant une validation explicite avant modification

- **Logique « employé actif »** : confirmé avec l'utilisateur que la reformulation ne devait pas changer le comportement (mise à pied = toujours actif).
- **Format de date** : confirmé qu'un composant personnalisé (plutôt que les champs natifs) était voulu, après analyse de l'absence de risque sur la logique.

## Fichiers modifiés

`src/steps/Step1Employee.tsx`, `Step2Cessation.tsx`, `Step3Access.tsx`, `Step4Equipment.tsx`, `Step5Applications.tsx`,
`src/components/RegleDePayeSelect.tsx` (nouveau), `src/components/DateInput.tsx` (nouveau), `src/components/ChoiceCard.tsx`,
`src/components/SummarySidebar.tsx`, `src/components/icons.tsx`, `src/data/catalogs.ts`, `src/App.css`
