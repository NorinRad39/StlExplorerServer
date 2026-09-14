#!/bin/sh
# ============================================================================
#  Mise a jour automatique du serveur STL Explorer sur le NAS Synology.
#
#  Surveille /volume1/Applications/STLExplorer/Serveur/serveur.json : des que la
#  version annoncee differe de celle installee, charge la nouvelle image Docker
#  et redemarre le conteneur.
#
#  Installation (voir README-NAS.md) :
#    - script depose dans /volume1/Applications/STLExplorer/Serveur/
#    - tache planifiee DSM (utilisateur root) toutes les 5 minutes :
#        /volume1/Applications/STLExplorer/Serveur/stlexplorer-autoupdate.sh
#
#  Options :
#    --maintenant   installe meme si la version annoncee est deja marquee installee
#                   (utilise par deploy-serveur.ps1 pour un deploiement immediat)
# ============================================================================

set -eu

DOSSIER_PARTAGE="/volume1/Applications/STLExplorer"
DOSSIER="$DOSSIER_PARTAGE/Serveur"
MARQUEUR_PUBLIE="$DOSSIER/serveur.json"
MARQUEUR_INSTALLE="$DOSSIER/.version-installee"
FICHIER_OVERRIDE="$DOSSIER/compose-override.yml"
JOURNAL="$DOSSIER/autoupdate.log"
CONTENEUR="stlexplorer-api"
# Emplacement connu du projet Compose, utilise si les etiquettes du conteneur manquent.
PROJET_COMPOSE_DEFAUT="/volume1/docker/STLExplorer/compose.yaml"

FORCER=0
DIAGNOSTIC_SEUL=0
case "${1:-}" in
    --maintenant)  FORCER=1 ;;
    --diagnostic)  DIAGNOSTIC_SEUL=1 ;;
esac

log() {
    echo "$(date '+%Y-%m-%d %H:%M:%S')  $*" >> "$JOURNAL"
    echo "$*"
}

# Etat reel du montage des mises a jour, cote hote et cote conteneur.
# Journalise apres chaque reconfiguration : sans acces SSH, c'est le seul moyen
# de savoir pourquoi /api/Update/manifest ne trouve pas son fichier.
diagnostiquer() {
    log "DIAG partage hote  : $(ls -ld /volume*/Applications/STLExplorer 2>&1 | tr '\n' ' ')"
    log "DIAG manifeste     : $(ls -l /volume*/Applications/STLExplorer/update.json 2>&1 | tr '\n' ' ')"
    # Comparaison avec le partage Maquette, qui lui est bien lu par l'application :
    # une difference de droits entre les deux explique un /data/updates inaccessible.
    log "DIAG partage OK    : $(ls -ld /volume1/Maquette/3D_Maquettes 2>&1 | tr '\n' ' ')"
    log "DIAG utilisateur   : $("$DOCKER" exec "$CONTENEUR" id 2>&1 | tr '\n' ' ')"
    log "DIAG montages      : $("$DOCKER" inspect "$CONTENEUR" \
        --format '{{ range .Mounts }}{{ .Source }}=>{{ .Destination }} {{ end }}' 2>&1)"
    log "DIAG vu du conteneur : $("$DOCKER" exec "$CONTENEUR" ls -la /data/updates 2>&1 \
        | tr '\n' ' ' | cut -c1-400)"
}

# Le binaire docker de Container Manager n'est pas toujours dans le PATH des taches planifiees.
DOCKER=""
for candidat in /usr/local/bin/docker \
                /var/packages/ContainerManager/target/usr/bin/docker \
                /var/packages/Docker/target/usr/bin/docker \
                "$(command -v docker 2>/dev/null || true)"; do
    if [ -n "$candidat" ] && [ -x "$candidat" ]; then
        DOCKER="$candidat"
        break
    fi
done

if [ -z "$DOCKER" ]; then
    log "ERREUR : binaire docker introuvable."
    exit 1
fi

# Compose v2 (« docker compose ») sur Container Manager, v1 (« docker-compose ») sur l'ancien paquet Docker.
COMPOSE=""
if "$DOCKER" compose version >/dev/null 2>&1; then
    COMPOSE="$DOCKER compose"
else
    for candidat in /usr/local/bin/docker-compose \
                    /var/packages/ContainerManager/target/usr/bin/docker-compose \
                    /var/packages/Docker/target/usr/bin/docker-compose \
                    "$(command -v docker-compose 2>/dev/null || true)"; do
        if [ -n "$candidat" ] && [ -x "$candidat" ]; then
            COMPOSE="$candidat"
            break
        fi
    done
fi

if [ "$DIAGNOSTIC_SEUL" -eq 1 ]; then
    log "--- Diagnostic demande ---"
    diagnostiquer
    exit 0
fi

# Rien de publie : sortie silencieuse (cas normal entre deux deploiements).
[ -f "$MARQUEUR_PUBLIE" ] || exit 0

lire_json() {
    # Extraction simple d'une valeur texte : evite de dependre de jq, absent de DSM.
    sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" "$MARQUEUR_PUBLIE" | head -n 1
}

VERSION_PUBLIEE=$(lire_json version)
FICHIER_TAR=$(lire_json fichier)

if [ -z "$VERSION_PUBLIEE" ] || [ -z "$FICHIER_TAR" ]; then
    log "ERREUR : serveur.json illisible (version ou fichier manquant)."
    exit 1
fi

VERSION_INSTALLEE=""
[ -f "$MARQUEUR_INSTALLE" ] && VERSION_INSTALLEE=$(cat "$MARQUEUR_INSTALLE")

# Le dossier des mises a jour doit etre monte dans le conteneur, sinon l'API ne peut
# pas distribuer les APK / setups aux clients (/api/Update/manifest renvoie 404).
# On verifie a chaque passage : un redemarrage du projet depuis Container Manager
# recreerait le conteneur sans la surcouche, il faut alors la reappliquer.
MONTAGE_ABSENT=0
if ! "$DOCKER" inspect "$CONTENEUR" \
        --format '{{ range .Mounts }}{{ .Destination }} {{ end }}' 2>/dev/null \
        | grep -q "/data/updates"; then
    MONTAGE_ABSENT=1
fi

# Le montage peut exister sans etre exploitable : l'application tourne sous un
# utilisateur non-root, et un partage dont les droits ne lui accordent pas la
# lecture donne un /data/updates vide vu du conteneur.
MANIFESTE_LISIBLE=0
if [ "$MONTAGE_ABSENT" -eq 0 ] \
   && "$DOCKER" exec "$CONTENEUR" test -r /data/updates/update.json 2>/dev/null; then
    MANIFESTE_LISIBLE=1
fi

MARQUEUR_DIAG="$DOSSIER/.diagnostic-fait"
[ "$MANIFESTE_LISIBLE" -eq 1 ] && rm -f "$MARQUEUR_DIAG"

# L'application tourne sous l'utilisateur "app" (uid 1654), qui n'est ni proprietaire
# ni membre du groupe du partage. Sans droit de lecture pour les "autres", le dossier
# monte reste inaccessible : on le corrige, comme l'est deja le partage Maquette.
if [ "$MONTAGE_ABSENT" -eq 0 ] && [ "$MANIFESTE_LISIBLE" -eq 0 ]; then
    log "Lecture impossible depuis le conteneur : correction des droits sur $DOSSIER_PARTAGE"

    chmod -R o+rX "$DOSSIER_PARTAGE" >> "$JOURNAL" 2>&1 || log "ERREUR : chmod a echoue."

    # Sur DSM, une ACL Synology (le « + » dans les droits) peut neutraliser chmod.
    if ! "$DOCKER" exec "$CONTENEUR" test -r /data/updates/update.json 2>/dev/null; then
        if command -v synoacltool >/dev/null 2>&1; then
            log "Droits toujours refuses : ajout d'une ACL Synology en lecture."
            synoacltool -add "$DOSSIER_PARTAGE" "everyone:allow:r-x---a-R--:fd--" \
                >> "$JOURNAL" 2>&1 || log "ERREUR : synoacltool a echoue."
        fi
    fi

    if "$DOCKER" exec "$CONTENEUR" test -r /data/updates/update.json 2>/dev/null; then
        MANIFESTE_LISIBLE=1
        rm -f "$MARQUEUR_DIAG"
        log "Droits corriges : le manifeste est desormais lisible par l'application."
    fi
fi

if [ "$VERSION_PUBLIEE" = "$VERSION_INSTALLEE" ] && [ "$FORCER" -eq 0 ] && [ "$MONTAGE_ABSENT" -eq 0 ]; then
    # Montage en place mais manifeste illisible : recreer le conteneur n'y changerait
    # rien (c'est un probleme de droits ou de chemin), on journalise donc une seule
    # fois de quoi identifier la cause, sans boucler toutes les 5 minutes.
    if [ "$MANIFESTE_LISIBLE" -eq 0 ] && [ ! -f "$MARQUEUR_DIAG" ]; then
        log "Montage present mais /data/updates/update.json illisible depuis le conteneur."
        diagnostiquer
        : > "$MARQUEUR_DIAG"
    fi
    exit 0   # deja a jour
fi

CHEMIN_TAR="$DOSSIER/$FICHIER_TAR"
if [ ! -f "$CHEMIN_TAR" ]; then
    log "ERREUR : paquet annonce mais introuvable : $CHEMIN_TAR"
    exit 1
fi

if [ "$VERSION_PUBLIEE" != "$VERSION_INSTALLEE" ]; then
    log "Mise a jour detectee : ${VERSION_INSTALLEE:-aucune} -> $VERSION_PUBLIEE"
elif [ "$MONTAGE_ABSENT" -eq 1 ]; then
    log "Montage /data/updates absent : reapplication de la configuration."
fi

# Retrouver le projet Compose a partir du conteneur en cours d'execution :
# evite de coder en dur un chemin qui depend de Container Manager.
FICHIER_COMPOSE=$("$DOCKER" inspect "$CONTENEUR" \
    --format '{{ index .Config.Labels "com.docker.compose.project.config_files" }}' 2>/dev/null || true)
DOSSIER_COMPOSE=$("$DOCKER" inspect "$CONTENEUR" \
    --format '{{ index .Config.Labels "com.docker.compose.project.working_dir" }}' 2>/dev/null || true)
NOM_PROJET=$("$DOCKER" inspect "$CONTENEUR" \
    --format '{{ index .Config.Labels "com.docker.compose.project" }}' 2>/dev/null || true)

# Repli sur l'emplacement connu du projet : les etiquettes Compose disparaissent
# si le conteneur a ete recree autrement (docker run, restauration DSM...).
if [ -z "$FICHIER_COMPOSE" ] || [ ! -f "$FICHIER_COMPOSE" ]; then
    if [ -f "$PROJET_COMPOSE_DEFAUT" ]; then
        log "Etiquettes Compose absentes, utilisation de $PROJET_COMPOSE_DEFAUT"
        FICHIER_COMPOSE="$PROJET_COMPOSE_DEFAUT"
        DOSSIER_COMPOSE=$(dirname "$PROJET_COMPOSE_DEFAUT")
    fi
fi

if [ "$VERSION_PUBLIEE" != "$VERSION_INSTALLEE" ]; then
    log "Chargement de l'image depuis $FICHIER_TAR"
    if ! "$DOCKER" load -i "$CHEMIN_TAR" >> "$JOURNAL" 2>&1; then
        log "ERREUR : docker load a echoue."
        exit 1
    fi
fi

if [ -n "$FICHIER_COMPOSE" ] && [ -f "$FICHIER_COMPOSE" ] && [ -n "$COMPOSE" ]; then
    # « up -d » recree le conteneur car l'ID de l'image (ou la configuration) a change :
    # inutile d'arreter le projet a la main dans Container Manager.
    #
    # La surcouche compose-override.yml ajoute le montage /data/updates sans modifier
    # le fichier du projet. Les listes "volumes" des deux fichiers sont fusionnees.
    ARGS_COMPOSE="-f $FICHIER_COMPOSE"
    if [ -f "$FICHIER_OVERRIDE" ]; then
        ARGS_COMPOSE="$ARGS_COMPOSE -f $FICHIER_OVERRIDE"
    else
        log "ATTENTION : $FICHIER_OVERRIDE absent, le dossier de mises a jour ne sera pas monte."
    fi
    # Nom de projet impose : sans lui, Compose le deduirait du dossier courant et
    # creerait un second jeu de conteneurs a cote de celui de Container Manager.
    [ -n "$NOM_PROJET" ] && ARGS_COMPOSE="-p $NOM_PROJET $ARGS_COMPOSE"

    log "Application de la configuration via Compose : $FICHIER_COMPOSE"
    cd "${DOSSIER_COMPOSE:-$(dirname "$FICHIER_COMPOSE")}"
    # shellcheck disable=SC2086
    if ! $COMPOSE $ARGS_COMPOSE up -d >> "$JOURNAL" 2>&1; then
        log "ERREUR : docker compose up a echoue."
        exit 1
    fi
else
    # PAS de repli par « docker restart » : redemarrer un conteneur le relance avec
    # l'image qui a servi a le creer. La nouvelle image serait chargee puis ignoree,
    # et le script conclurait a tort a une mise a jour reussie. Mieux vaut echouer
    # bruyamment et laisser la version installee inchangee : la tache reessaiera.
    log "ERREUR : impossible de recreer le conteneur via Compose."
    log "  fichier compose : ${FICHIER_COMPOSE:-<label absent>}"
    log "  commande compose : ${COMPOSE:-<introuvable>}"
    log "  diagnostic : $("$DOCKER" compose version 2>&1 | head -n 1)"
    log "  L'image est chargee mais le conteneur tourne toujours sur l'ancienne."
    exit 1
fi

echo "$VERSION_PUBLIEE" > "$MARQUEUR_INSTALLE"
log "Serveur mis a jour en version $VERSION_PUBLIEE"

# Verifier que la reconfiguration a bien pris : si le montage manque toujours,
# le diagnostic dit ou ca coince (chemin de partage, fusion Compose...).
if ! "$DOCKER" inspect "$CONTENEUR" \
        --format '{{ range .Mounts }}{{ .Destination }} {{ end }}' 2>/dev/null \
        | grep -q "/data/updates"; then
    log "ATTENTION : /data/updates toujours absent apres reconfiguration."
    diagnostiquer
fi

# Menage : ne garder que les images sans tag laissees par les chargements successifs.
"$DOCKER" image prune -f >> "$JOURNAL" 2>&1 || true

exit 0
