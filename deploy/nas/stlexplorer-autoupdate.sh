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

DOSSIER="/volume1/Applications/STLExplorer/Serveur"
MARQUEUR_PUBLIE="$DOSSIER/serveur.json"
MARQUEUR_INSTALLE="$DOSSIER/.version-installee"
JOURNAL="$DOSSIER/autoupdate.log"
CONTENEUR="stlexplorer-api"

FORCER=0
[ "${1:-}" = "--maintenant" ] && FORCER=1

log() {
    echo "$(date '+%Y-%m-%d %H:%M:%S')  $*" >> "$JOURNAL"
    echo "$*"
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

if [ "$VERSION_PUBLIEE" = "$VERSION_INSTALLEE" ] && [ "$FORCER" -eq 0 ]; then
    exit 0   # deja a jour
fi

CHEMIN_TAR="$DOSSIER/$FICHIER_TAR"
if [ ! -f "$CHEMIN_TAR" ]; then
    log "ERREUR : paquet annonce mais introuvable : $CHEMIN_TAR"
    exit 1
fi

log "Mise a jour detectee : ${VERSION_INSTALLEE:-aucune} -> $VERSION_PUBLIEE"

# Retrouver le projet Compose a partir du conteneur en cours d'execution :
# evite de coder en dur un chemin qui depend de Container Manager.
FICHIER_COMPOSE=$("$DOCKER" inspect "$CONTENEUR" \
    --format '{{ index .Config.Labels "com.docker.compose.project.config_files" }}' 2>/dev/null || true)
DOSSIER_COMPOSE=$("$DOCKER" inspect "$CONTENEUR" \
    --format '{{ index .Config.Labels "com.docker.compose.project.working_dir" }}' 2>/dev/null || true)

log "Chargement de l'image depuis $FICHIER_TAR"
if ! "$DOCKER" load -i "$CHEMIN_TAR" >> "$JOURNAL" 2>&1; then
    log "ERREUR : docker load a echoue."
    exit 1
fi

if [ -n "$FICHIER_COMPOSE" ] && [ -f "$FICHIER_COMPOSE" ] && [ -n "$COMPOSE" ]; then
    # « up -d » recree le conteneur car l'ID de l'image a change : inutile d'arreter
    # le projet a la main dans Container Manager.
    log "Redemarrage via Compose : $FICHIER_COMPOSE"
    cd "${DOSSIER_COMPOSE:-$(dirname "$FICHIER_COMPOSE")}"
    if ! $COMPOSE -f "$FICHIER_COMPOSE" up -d >> "$JOURNAL" 2>&1; then
        log "ERREUR : docker compose up a echoue."
        exit 1
    fi
else
    # Repli si le conteneur n'a pas ete cree par Compose : simple recreation.
    log "Projet Compose introuvable, redemarrage direct du conteneur."
    if ! "$DOCKER" restart "$CONTENEUR" >> "$JOURNAL" 2>&1; then
        log "ERREUR : redemarrage du conteneur impossible."
        exit 1
    fi
fi

echo "$VERSION_PUBLIEE" > "$MARQUEUR_INSTALLE"
log "Serveur mis a jour en version $VERSION_PUBLIEE"

# Menage : ne garder que les images sans tag laissees par les chargements successifs.
"$DOCKER" image prune -f >> "$JOURNAL" 2>&1 || true

exit 0
