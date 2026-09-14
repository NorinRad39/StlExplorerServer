#!/bin/sh
# ============================================================================
#  Renouvellement automatique du certificat wildcard *.file4all.fr
#
#  Remplace la procedure manuelle (certbot --manual + pose du TXT dans la zone
#  + import des PEM dans DSM) par une chaine entierement automatique :
#    - challenge DNS-01 pose et retire via l'API Cloudflare ;
#    - import du certificat dans DSM et rechargement de nginx (reverse proxy)
#      par le hook synology_dsm d'acme.sh.
#
#  Le certificat etant un wildcard, DNS-01 est obligatoire : le Let's Encrypt
#  integre a DSM (HTTP-01, port 80 ouvert sur Internet) ne peut pas convenir.
#
#  Installation initiale (une seule fois, en root) :
#      sh /volume1/Applications/acme/renouveler-certificat.sh --installer
#
#  Ensuite, tache planifiee DSM (utilisateur root, une fois par jour) :
#      sh /volume1/Applications/acme/renouveler-certificat.sh
#
#  acme.sh ne renouvelle qu'a 60 jours : un passage quotidien ne sollicite pas
#  Let's Encrypt inutilement, il ne fait rien tant que ce n'est pas necessaire.
# ============================================================================

set -eu

BASE="/volume1/Applications/acme"
SECRETS="$BASE/secrets-certificat.env"
ACME_HOME="$BASE/.acme.sh"
ACME="$ACME_HOME/acme.sh"
JOURNAL="$BASE/certificat.log"

DOMAINE="file4all.fr"
JOKER="*.file4all.fr"

log() {
    echo "$(date '+%Y-%m-%d %H:%M:%S')  $*" >> "$JOURNAL"
    echo "$*"
}

if [ ! -f "$SECRETS" ]; then
    log "ERREUR : $SECRETS introuvable (voir secrets-certificat.env.exemple)."
    exit 1
fi

# CF_Token, SYNO_Username, SYNO_Password... Ce fichier ne doit jamais rejoindre
# le depot git ni un dossier lisible par tous.
# shellcheck disable=SC1090
. "$SECRETS"

# acme.sh est lance comme processus separe : il ne recoit que les variables
# EXPORTEES. Sans cela, le plugin Cloudflare repond « You didn't specify a
# Cloudflare api key » alors que le fichier de secrets est bien renseigne.
exporter_si_defini() {
    for nom in "$@"; do
        eval "valeur=\${$nom:-}"
        [ -n "$valeur" ] && export "$nom"
    done
    return 0
}

exporter_si_defini \
    CF_Token CF_Account_ID CF_Zone_ID CF_Key CF_Email \
    SYNO_Username SYNO_Password SYNO_Certificate SYNO_Create \
    SYNO_Scheme SYNO_Hostname SYNO_Port SYNO_TOTP_SECRET \
    SYNO_DID SYNO_Device_ID SYNO_Device_Name

export ACME_HOME
export LE_WORKING_DIR="$ACME_HOME"

installer_acme() {
    log "Installation d'acme.sh dans $ACME_HOME"
    mkdir -p "$BASE"
    chmod 700 "$BASE"

    tmp=$(mktemp -d)

    # On installe depuis l'archive plutot que par « curl https://get.acme.sh | sh » :
    # ce lanceur n'accepte les options que sous la forme « sh -s email=... » et
    # deforme les arguments en --home (il produit « ----home »).
    if ! curl -fsSL https://github.com/acmesh-official/acme.sh/archive/master.tar.gz \
            -o "$tmp/acme.tar.gz"; then
        log "ERREUR : telechargement d'acme.sh impossible."
        rm -rf "$tmp"
        exit 1
    fi

    tar xzf "$tmp/acme.tar.gz" -C "$tmp"

    # --home : garder acme.sh sur le volume de donnees, une mise a jour DSM
    #          pouvant remettre a plat les repertoires systeme.
    # --nocron : la planification est assuree par le Planificateur de taches DSM,
    #            plus fiable ici que la crontab, que DSM peut reecrire.
    # Invocation par « sh » : le fichier extrait de l'archive n'a pas forcement le
    # bit executable, et /tmp peut etre monte noexec sur DSM.
    if ! (cd "$tmp/acme.sh-master" && sh ./acme.sh --install \
            --home "$ACME_HOME" \
            --accountemail "${ACME_EMAIL:-}" \
            --nocron) >> "$JOURNAL" 2>&1; then
        log "ERREUR : l'installation d'acme.sh a echoue."
        rm -rf "$tmp"
        exit 1
    fi

    rm -rf "$tmp"

    if [ ! -f "$ACME" ]; then
        log "ERREUR : acme.sh introuvable apres installation ($ACME)."
        exit 1
    fi
    chmod +x "$ACME" 2>/dev/null || true

    # acme.sh utilise ZeroSSL par defaut : on reste sur Let's Encrypt, l'autorite
    # deja en place et reconnue par Android.
    sh "$ACME" --home "$ACME_HOME" --set-default-ca --server letsencrypt >> "$JOURNAL" 2>&1

    log "acme.sh installe."
}

emettre_certificat() {
    log "Emission du certificat $DOMAINE + $JOKER (DNS-01 via Cloudflare)"
    sh "$ACME" --home "$ACME_HOME" --issue --server letsencrypt \
        --dns dns_cf -d "$DOMAINE" -d "$JOKER" >> "$JOURNAL" 2>&1
    log "Certificat obtenu."
}

deployer_dans_dsm() {
    log "Import du certificat dans DSM"
    # Le hook memorise ses parametres : les renouvellements suivants le rejouent seuls.
    sh "$ACME" --home "$ACME_HOME" --deploy -d "$DOMAINE" --deploy-hook synology_dsm \
        >> "$JOURNAL" 2>&1
    log "Certificat importe dans DSM et services recharges."
}

if [ "${1:-}" = "--installer" ]; then
    [ -f "$ACME" ] || installer_acme
    emettre_certificat
    deployer_dans_dsm
    log "Installation terminee. Planifie ce script quotidiennement en root."
    exit 0
fi

if [ ! -f "$ACME" ]; then
    log "ERREUR : acme.sh absent. Lance d'abord ce script avec --installer."
    exit 1
fi

# Passage courant : acme.sh verifie l'echeance, renouvelle si besoin (60 jours)
# et rejoue le hook de deploiement. Sans rien a faire, il sort en silence.
sh "$ACME" --home "$ACME_HOME" --cron >> "$JOURNAL" 2>&1 || {
    log "ERREUR : le passage de renouvellement a echoue (voir ci-dessus)."
    exit 1
}

# Trace de l'echeance, pour reperer une panne de renouvellement sans fouiller.
FIN=$(echo | openssl s_client -connect "stl.$DOMAINE:443" -servername "stl.$DOMAINE" 2>/dev/null \
      | openssl x509 -noout -enddate 2>/dev/null | cut -d= -f2)
[ -n "$FIN" ] && log "Certificat en service valable jusqu'au : $FIN"

exit 0
