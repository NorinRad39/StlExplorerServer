# Configuration du NAS (à faire une seule fois)

Ces trois étapes rendent les déploiements entièrement automatiques : ensuite,
`deploy\deploy-serveur.ps1` construit, publie **et installe** la nouvelle version
du serveur sans intervention.

Tout se fait depuis un terminal sur le PC. Le mot de passe du NAS est demandé
une fois par étape — c'est pour cela que ces commandes ne sont pas scriptées.

---

## 1. Monter le dossier de mises à jour dans le conteneur

Sans ça, l'API ne peut pas distribuer les APK / setups aux clients
(`/api/Update/manifest` renvoie 404).

Dans **Container Manager → Projet → stlexplorer → Modifier**, ajouter cette ligne
aux `volumes:` du service `stlexplorer-api` :

```yaml
      - /volume1/Applications/STLExplorer:/data/updates:ro
```

Puis enregistrer et reconstruire le projet. La version de référence du fichier
est [docker-compose.yml](../../docker-compose.yml) à la racine du dépôt.

---

## 2. Autoriser le PC à déployer sans mot de passe (SSH)

SSH doit être activé : **Panneau de configuration → Terminal & SNMP → Activer le service SSH**.

Dans PowerShell sur le PC (le mot de passe du NAS est demandé une fois) :

```powershell
type $env:USERPROFILE\.ssh\nas_stlexplorer.pub | ssh florent@192.168.20.253 "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 700 ~/.ssh && chmod 600 ~/.ssh/authorized_keys && chmod 700 ~"
```

Vérification (doit répondre `OK` sans demander de mot de passe) :

```powershell
ssh -i $env:USERPROFILE\.ssh\nas_stlexplorer florent@192.168.20.253 "echo OK"
```

> Les scripts passent la clé explicitement (`-i`), il n'y a donc aucun
> `~/.ssh/config` à maintenir. L'utilisateur et l'hôte sont définis à un seul
> endroit, en haut de [`deploy\_commun.ps1`](../_commun.ps1).

### Autoriser le script de mise à jour sans mot de passe

Les commandes Docker exigent `sudo` sur DSM. Se connecter puis :

```bash
ssh -i ~/.ssh/nas_stlexplorer florent@192.168.20.253
sudo sh -c 'echo "florent ALL=(ALL) NOPASSWD: /volume1/Applications/STLExplorer/Serveur/stlexplorer-autoupdate.sh" > /etc/sudoers.d/stlexplorer && chmod 440 /etc/sudoers.d/stlexplorer'
```

> À noter : le script d'auto-update lance des commandes Docker en root. Lui accorder
> `NOPASSWD` revient donc, en pratique, à accorder un accès root au NAS à quiconque
> peut exécuter ce script ou modifier le fichier sur le partage. C'est acceptable sur
> un réseau domestique ; à éviter si le partage `Applications` est accessible largement.

---

## 3. Installer la mise à jour automatique du serveur

Le script [stlexplorer-autoupdate.sh](stlexplorer-autoupdate.sh) est déjà copié par
`deploy-serveur.ps1` dans `/volume1/Applications/STLExplorer/Serveur/`.

Créer la tâche planifiée dans **DSM → Panneau de configuration → Planificateur
de tâches → Créer → Tâche planifiée → Script défini par l'utilisateur** :

| Champ | Valeur |
|---|---|
| Tâche | `STL Explorer - mise à jour serveur` |
| Utilisateur | `root` |
| Programmation | Quotidien, répéter **toutes les 5 minutes** |
| Script | `sh /volume1/Applications/STLExplorer/Serveur/stlexplorer-autoupdate.sh` |

> Le `sh` en préfixe évite d'avoir à rendre le script exécutable (`chmod +x`),
> ce qui n'est pas possible depuis un partage SMB monté sur Windows.

Pour installer sans attendre les 5 minutes : sélectionner la tâche puis
**Exécuter**. C'est aussi la façon la plus simple de mettre à jour le serveur
sans passer par Container Manager — le script recrée le conteneur via Compose,
**il n'y a jamais besoin d'arrêter le projet à la main**.

À partir de là, déposer un nouveau paquet suffit : le NAS détecte le changement de
version dans `serveur.json`, charge l'image et redémarre le conteneur.

---

## Fonctionnement

```
deploy-serveur.ps1                        NAS (tâche planifiée, toutes les 5 min)
  ├── docker compose build                  ├── lit Serveur/serveur.json
  ├── docker save → Serveur/*.tar           ├── version différente de .version-installee ?
  ├── écrit Serveur/serveur.json            ├── docker load -i *.tar
  └── ssh → installation immédiate          └── docker compose up -d
       (si SSH configuré)
```

Journal des mises à jour côté NAS : `/volume1/Applications/STLExplorer/Serveur/autoupdate.log`

## Dépannage

| Symptôme | Cause probable |
|---|---|
| `/api/Update/manifest` renvoie 404 | Le volume de l'étape 1 n'est pas monté |
| Le NAS n'installe rien | Tâche planifiée absente, ou script non exécutable (`chmod +x`) |
| `deploy-serveur.ps1` dit « SSH non configuré » | Étape 2 incomplète : clé publique absente de `~/.ssh/authorized_keys` sur le NAS |
| `sudo: a password is required` | La règle sudoers de l'étape 2 n'est pas en place |
