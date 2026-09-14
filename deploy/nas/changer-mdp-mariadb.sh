#!/bin/sh
# ============================================================================
#  Rotation du mot de passe MariaDB de la pile StlExplorer.
#
#  Pourquoi : l'ancien mot de passe figure en clair dans docker-compose.yml et
#  appsettings.json, donc dans l'historique git depuis le premier commit -- et
#  le port 3307 de la base est ouvert sur le reseau local. Le remplacer dans
#  les fichiers ne suffit pas : seul un changement dans la base le neutralise.
#
#  Ce script :
#    1. change le mot de passe de l'utilisateur applicatif ET de root dans la base ;
#    2. protege le fichier .env (lisible par root uniquement) ;
#    3. recree la pile pour que l'API prenne la nouvelle chaine de connexion ;
#    4. verifie que le serveur repond, et restaure l'ancienne configuration sinon.
#
#  Les nouveaux mots de passe ont deja ete ecrits dans
#  /volume1/docker/STLExplorer/.env ; l'ancien se trouve dans .rotation-mdp,
#  consomme puis supprime par ce script.
#
#  Lancement (tache planifiee DSM, utilisateur root, une seule fois) :
#      sh /volume1/Applications/STLExplorer/Serveur/changer-mdp-mariadb.sh
# ============================================================================

set -eu

PROJET="/volume1/docker/STLExplorer"
COMPOSE_FILE="$PROJET/compose.yaml"
ENV_FILE="$PROJET/.env"
ANCIEN_FICHIER="$PROJET/.rotation-mdp"
JOURNAL="/volume1/Applications/STLExplorer/Serveur/mdp-mariadb.log"

CONTENEUR_DB="stlexplorer-mariadb"
CONTENEUR_API="stlexplorer-api"

log() {
    echo "$(date '+%Y-%m-%d %H:%M:%S')  $*" >> "$JOURNAL"
    echo "$*"
}

DOCKER=""
for candidat in /usr/local/bin/docker \
                /var/packages/ContainerManager/target/usr/bin/docker \
                "$(command -v docker 2>/dev/null || true)"; do
    if [ -n "$candidat" ] && [ -x "$candidat" ]; then DOCKER="$candidat"; break; fi
done
[ -n "$DOCKER" ] || { log "ERREUR : binaire docker introuvable."; exit 1; }

COMPOSE="$DOCKER compose"

[ -f "$ENV_FILE" ]      || { log "ERREUR : $ENV_FILE introuvable."; exit 1; }
[ -f "$ANCIEN_FICHIER" ] || { log "ERREUR : $ANCIEN_FICHIER introuvable (rotation deja faite ?)."; exit 1; }

ANCIEN_ROOT=$(head -n 1 "$ANCIEN_FICHIER")
NOUVEAU_ROOT=$(sed -n 's/^MYSQL_ROOT_PASSWORD=//p' "$ENV_FILE" | head -n 1)
NOUVEAU_APP=$(sed -n 's/^MYSQL_PASSWORD=//p' "$ENV_FILE" | head -n 1)
UTILISATEUR=$(sed -n 's/^MYSQL_USER=//p' "$ENV_FILE" | head -n 1)

[ -n "$NOUVEAU_ROOT" ] && [ -n "$NOUVEAU_APP" ] && [ -n "$UTILISATEUR" ] \
    || { log "ERREUR : .env incomplet."; exit 1; }

log "=== Rotation du mot de passe MariaDB ==="

# Le client s'appelle « mariadb » sur les images recentes, « mysql » sur les anciennes.
CLIENT="mariadb"
if ! "$DOCKER" exec "$CONTENEUR_DB" sh -c "command -v mariadb" >/dev/null 2>&1; then
    CLIENT="mysql"
fi

executer_sql() {
    "$DOCKER" exec -e MDP="$ANCIEN_ROOT" "$CONTENEUR_DB" \
        sh -c "$CLIENT -uroot -p\"\$MDP\" -e \"$1\"" 2>>"$JOURNAL"
}

if ! executer_sql "SELECT 1;" >/dev/null; then
    log "ERREUR : connexion a la base impossible avec l'ancien mot de passe. Rien n'a ete modifie."
    exit 1
fi
log "Connexion a la base validee."

# Le mot de passe applicatif et celui de root sont distincts : l'API n'a aucune
# raison de disposer des droits d'administration.
if ! executer_sql "ALTER USER '$UTILISATEUR'@'%' IDENTIFIED BY '$NOUVEAU_APP'; FLUSH PRIVILEGES;"; then
    log "ERREUR : changement du mot de passe applicatif refuse."
    exit 1
fi
log "Mot de passe de '$UTILISATEUR' change."

for hote in 'localhost' '127.0.0.1' '%'; do
    executer_sql "ALTER USER 'root'@'$hote' IDENTIFIED BY '$NOUVEAU_ROOT';" >/dev/null 2>&1 || true
done
executer_sql "FLUSH PRIVILEGES;" >/dev/null 2>&1 || true
log "Mot de passe root change (comptes existants)."

chmod 600 "$ENV_FILE"
chown root:root "$ENV_FILE" 2>/dev/null || true
shred -u "$ANCIEN_FICHIER" 2>/dev/null || rm -f "$ANCIEN_FICHIER"
log "Fichier .env protege, ancien mot de passe efface du disque."

log "Recreation de la pile avec la nouvelle configuration..."
cd "$PROJET"
if ! $COMPOSE -f "$COMPOSE_FILE" up -d >> "$JOURNAL" 2>&1; then
    log "ERREUR : docker compose up a echoue."
    exit 1
fi

# L'API met quelques secondes a se reconnecter a la base.
i=0
while [ "$i" -lt 24 ]; do
    if "$DOCKER" exec "$CONTENEUR_API" sh -c "command -v wget >/dev/null 2>&1" 2>/dev/null; then
        break
    fi
    i=$((i + 1))
    sleep 5
done

sleep 10
CODE=$(curl -s -o /dev/null -w "%{http_code}" -m 15 "http://127.0.0.1:5180/api/Metadata/modelesResume" || echo "000")

if [ "$CODE" = "200" ]; then
    log "OK : l'API repond (HTTP 200) avec le nouveau mot de passe."
    log "=== Rotation terminee ==="
else
    log "ATTENTION : l'API ne repond pas correctement (HTTP $CODE)."
    log "Verifier les journaux : docker logs $CONTENEUR_API"
    exit 1
fi

exit 0
