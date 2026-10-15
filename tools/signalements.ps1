<#
.SYNOPSIS
    Récupère les signalements de Cycle Emploi pour une session Claude, et en ferme le suivi.

.DESCRIPTION
    Sans paramètre : télécharge les signalements OUVERTS (NEW, IN_PROGRESS) dans
    signalements\<id>\ — le signalement en Markdown (signalement.md) et ses
    captures (capture-1.png…) — puis affiche la liste. Une session Claude ouverte
    sur le dépôt peut alors les lire directement, captures comprises.

    Avec -Id et -Statut : met à jour le suivi d'un signalement, par exemple une
    fois le correctif déployé. -Travaux est OBLIGATOIRE pour FIXED : décris ce qui
    a été modifié (fichiers, logique, raison), pas seulement le résultat — c'est
    le journal technique de la correction, conservé avec le signalement.

    Exige PowerShell 7 et un compte administrateur de Cycle Emploi (Administration > Administrateurs) :
    l'authentification Windows de la session est utilisée telle quelle.

.EXAMPLE
    .\tools\signalements.ps1
    Télécharge les signalements ouverts.

.EXAMPLE
    .\tools\signalements.ps1 -Id 12 -Statut IN_PROGRESS
    Indique qu'on travaille dessus.

.EXAMPLE
    .\tools\signalements.ps1 -Id 12 -Statut FIXED -Commit 090aaaf `
        -Resolution "La date est maintenant validée." `
        -Travaux "src/steps/Step1Employee.tsx : le champ date est validé avant Suivant ; backend RequestsController : même contrôle côté serveur."
    Ferme le signalement ; la résolution est visible par la personne qui l'a fait.

.EXAMPLE
    .\tools\signalements.ps1 -Id 12 -Statut FIXED -Commit 090aaaf -Resolution "…" -TravauxFichier .\signalements\12\travaux.md
    Même chose, les travaux lus depuis un fichier Markdown (pratique quand ils sont longs).
#>
[CmdletBinding(DefaultParameterSetName = 'Recuperer')]
param(
    [Parameter(ParameterSetName = 'Recuperer')]
    [ValidateSet('open', 'tous', 'NEW', 'IN_PROGRESS', 'FIXED', 'REJECTED', 'DUPLICATE')]
    [string] $Filtre = 'open',

    [Parameter(ParameterSetName = 'Suivi', Mandatory)]
    [int] $Id,

    [Parameter(ParameterSetName = 'Suivi', Mandatory)]
    [ValidateSet('NEW', 'IN_PROGRESS', 'FIXED', 'REJECTED', 'DUPLICATE')]
    [string] $Statut,

    [Parameter(ParameterSetName = 'Suivi')] [string] $Commit,
    # Ce qui a été fait : le texte que la personne qui a signalé lira.
    [Parameter(ParameterSetName = 'Suivi')] [string] $Resolution,
    # Ce qui a été MODIFIÉ (fichiers, logique, raison) : journal technique.
    # Obligatoire pour FIXED (ou -TravauxFichier).
    [Parameter(ParameterSetName = 'Suivi')] [string] $Travaux,
    [Parameter(ParameterSetName = 'Suivi')] [string] $TravauxFichier,

    # The API is its own IIS site (TremblantOnboardingApi :8091), HTTP only.
    [string] $Api = 'http://vm-trm-live:8091/api',
    [string] $Dossier = (Join-Path $PSScriptRoot '..\signalements')
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 requis (pwsh).' }

$commun = @{ UseDefaultCredentials = $true; AllowUnencryptedAuthentication = $true; TimeoutSec = 60 }

if ($PSCmdlet.ParameterSetName -eq 'Suivi') {
    if ($TravauxFichier) { $Travaux = [IO.File]::ReadAllText((Resolve-Path $TravauxFichier), [Text.Encoding]::UTF8) }
    if ($Statut -eq 'FIXED' -and -not $Travaux) {
        Write-Warning "FIXED sans -Travaux : le serveur refusera s'il n'y a pas déjà de travaux journalisés sur ce signalement."
    }
    $corps = @{ status = $Statut; resolution = $Resolution; commitRef = $Commit; workLog = $Travaux } | ConvertTo-Json
    $s = Invoke-RestMethod "$Api/issues/$Id" -Method Put -Body ([Text.Encoding]::UTF8.GetBytes($corps)) `
                           -ContentType 'application/json; charset=utf-8' @commun
    "Signalement #$($s.issueId) : $($s.status)" + $(if ($s.commitRef) { " (commit $($s.commitRef))" } else { '' })
    return
}

$requete = if ($Filtre -eq 'tous') { "$Api/issues" } else { "$Api/issues?status=$Filtre" }
# Assigner PUIS énumérer : dans le pipeline, pwsh 7 remet le tableau JSON comme UN seul
# objet (et @($null) compte un élément quand la liste est vide).
$reponse = Invoke-RestMethod $requete @commun
$liste = @($reponse | Where-Object { $_ })
if ($liste.Count -eq 0) { "Aucun signalement ($Filtre)."; return }

New-Item -ItemType Directory -Force -Path $Dossier | Out-Null
foreach ($s in $liste) {
    $rep = Join-Path $Dossier $s.issueId
    New-Item -ItemType Directory -Force -Path $rep | Out-Null

    $md = Invoke-WebRequest "$Api/issues/$($s.issueId)/markdown?captures=files" @commun
    [IO.File]::WriteAllText((Join-Path $rep 'signalement.md'), $md.Content, [Text.UTF8Encoding]::new($false))

    $ordre = 0
    foreach ($img in $s.images) {
        $ordre++
        $ext = switch ($img.mimeType) { 'image/jpeg' { '.jpg' } 'image/webp' { '.webp' } default { '.png' } }
        Invoke-WebRequest "$Api/issues/$($s.issueId)/images/$($img.imageId)" -OutFile (Join-Path $rep "capture-$ordre$ext") @commun | Out-Null
    }

    # Documents joints (PDF, Word, Excel) : document-N-<nom d'origine>, comme les nomme le Markdown.
    $ordre = 0
    foreach ($doc in @($s.files)) {
        if (-not $doc) { continue }
        $ordre++
        Invoke-WebRequest "$Api/issues/$($s.issueId)/files/$($doc.fileId)" -OutFile (Join-Path $rep "document-$ordre-$($doc.fileName)") @commun | Out-Null
    }
}

$liste | ForEach-Object {
    [pscustomobject]@{
        No       = $_.issueId
        Statut   = $_.status
        Type     = $_.type
        Captures = @($_.images).Count
        Documents = @($_.files | Where-Object { $_ }).Count
        Par      = $_.createdByName ?? $_.createdBySam
        Titre    = $_.title
    }
} | Format-Table -AutoSize
"Enregistrés dans $(Resolve-Path $Dossier)"
"Rappel : pour FIXED, passe -Travaux (ou -TravauxFichier) avec ce qui a été modifié — c'est le journal technique de la correction."
