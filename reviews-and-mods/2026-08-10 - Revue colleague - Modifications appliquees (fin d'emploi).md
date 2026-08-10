# Revue collègue — Formulaire de fin d'emploi — Modifications appliquées

Source : `2026-08-10 - Revue colleague - Formulaire de fin d'emploi.docx` (annotations sur captures d'écran des étapes 2 et 3 du volet cessation).

## Étape 2 — Détails de la cessation (`Step2Cessation.tsx`)

| # | Commentaire du relecteur | Statut |
|---|---|---|
| 1 | Reformuler les libellés en questions (ex. "Quelle est la dernière journée...") | ✅ Appliqué |
| 2 | Ajouter un indice de champ (exemple) sous "Précisions sur le motif" | ✅ Appliqué |
| 3 | Si "Mise à pied temporaire" : demander si la date de retour est connue avant d'afficher le sélecteur de date | ✅ Appliqué — porte Oui/Non ("La date de retour au travail est-elle connue?") avant de révéler "Date prévue de retour au travail" |
| 4 | Si "Démission volontaire" : demander si un préavis a été reçu | ✅ Appliqué — champ conditionnel "Préavis reçu?" |
| 5 | Si l'équipier n'est pas admissible à une réembauche : demander le motif | ✅ Appliqué — champ conditionnel "Motif de non-admissibilité à la réembauche" |

## Étape 3 — Commentaires et suivis (`Step3DepartmentComments.tsx`)

| # | Commentaire du relecteur | Statut |
|---|---|---|
| 1 | Renommer le titre de l'étape "Commentaires par département" → "Commentaires et suivis" | ✅ Appliqué (aussi dans la nav latérale) |
| 2 | Remplacer le badge texte "Confidentiel" par une icône + texte explicite | ✅ Appliqué — "Visible uniquement par les ressources humaines" avec icône cadenas |
| 3 | Ajouter un exemple sous chaque zone de commentaire | ✅ Appliqué (`.field-hint` sous chaque section) |
| 4 | Renommer "Puce d'accès" → "Carte ou puce d'accès" | ✅ Appliqué |
| 5 | Renommer "Redingote (...)" → "Uniformes et matériel à récupérer" | ✅ Appliqué |
| 6 | Ajouter une note explicative en bas de section | ✅ Appliqué |

## Étape 4 — Révision et soumission (`StepReviewOffboarding.tsx`)

- Ajout des lignes de révision conditionnelles correspondant aux nouveaux champs (date de retour connue/prévue, préavis reçu, motif de non-admissibilité), avec les mêmes conditions d'affichage qu'à l'étape 2.
- Renommage du titre de section et des libellés pour rester cohérent avec l'étape 2/3.
- Ajout d'un encadré "Important" rappelant que la soumission déclenche les processus de désactivation d'accès et de récupération de matériel.

## Décision validée avec l'utilisateur

- **Date de retour au travail (mise à pied temporaire)** : implémentée comme une porte Oui/Non ("La date de retour au travail est-elle connue?") plutôt qu'un simple champ date — le sélecteur de date n'apparaît que si "Oui" est sélectionné.

## Changements techniques associés

- Backend : 4 nouvelles colonnes sur `OffboardingDetails` (`DateRetourConnue`, `DateRetourTravail`, `PreavisRecu`, `MotifNonAdmissibilite`) via migration EF Core, exposées dans `RequestDto`/`UpdateRequestDto` et `RequestsController`.
- Frontend : nouveaux champs dans `OffboardingInfo` (types.ts), constantes de catalogue pour éviter les chaînes magiques (`RAISON_ARRET_MISE_A_PIED_TEMPORAIRE`, `RAISON_ARRET_DEMISSION_VOLONTAIRE`, `REEMBAUCHERIEZ_NON`), mapping dans `WizardContext.toUpdateDto()`.
- Vérifié en local (persistance DB confirmée par requête directe sur `EmployeeLifecycle.dbo.OffboardingDetails`), déployé sur IIS vm-trm-live (frontend :8090, API :8091), hash SHA256 identique entre le build local et les fichiers déployés.
