// ------------------------------------------------------------------
// Pre-traitement des 3MF "production" (Bambu Studio, Orca, certains Prusa...).
//
// Ces slicers n'ecrivent pas la geometrie dans 3D/3dmodel.model : l'objet y
// est un simple <component p:path="/3D/Objects/xxx.model" objectid="1"> qui
// renvoie vers un autre fichier du 3MF. Le ThreeMFLoader ne sait pas suivre
// p:path : il cherche l'objet 1 dans le fichier principal, ne le trouve pas et
// plante ("Cannot read properties of undefined (reading 'mesh')").
//
// aplatir3mf() rapatrie les objets externes dans le fichier principal (ids
// renumerotes pour eviter les collisions) et retire p:path, puis renvoie un
// 3MF equivalent que le loader sait lire. Un 3MF sans p:path est rendu tel quel.
//
// Limite assumee : les materiaux (pid/pindex) des objets externes sont ignores,
// leurs identifiants renvoient a des ressources du fichier d'origine.
// ------------------------------------------------------------------
import { unzipSync, zipSync } from './vendor/three/addons/libs/fflate.module.js';

const NS_CORE = 'http://schemas.microsoft.com/3dmanufacturing/core/2015/02';
const NS_PROD = 'http://schemas.microsoft.com/3dmanufacturing/production/2015/06';
const ATTRIBUTS_MATERIAU = ['pid', 'pindex', 'p1', 'p2', 'p3'];

function lireXml(octets) {
  const doc = new DOMParser().parseFromString(new TextDecoder().decode(octets), 'application/xml');
  if (doc.getElementsByTagName('parsererror').length) {
    throw new Error('XML invalide dans le fichier 3MF');
  }
  return doc;
}

// Chemin du modele principal, declare dans _rels/.rels (3D/3dmodel.model par defaut).
function cheminModelePrincipal(fichiers) {
  const rels = fichiers['_rels/.rels'];
  if (rels) {
    const cible = Array.from(lireXml(rels).getElementsByTagName('Relationship'))
      .find(r => /3dmodel$/i.test(r.getAttribute('Type') || ''));
    if (cible) return cible.getAttribute('Target').replace(/^\//, '');
  }
  return '3D/3dmodel.model';
}

export function aplatir3mf(buffer) {
  const fichiers = unzipSync(new Uint8Array(buffer));
  const chemin = cheminModelePrincipal(fichiers);
  if (!fichiers[chemin]) return buffer;

  const doc = lireXml(fichiers[chemin]);
  const composants = Array.from(doc.getElementsByTagNameNS(NS_CORE, 'component'))
    .filter(c => c.getAttributeNS(NS_PROD, 'path'));
  if (composants.length === 0) return buffer;

  const ressources = doc.getElementsByTagNameNS(NS_CORE, 'resources')[0];
  let prochainId = 1 + Math.max(0, ...Array.from(ressources.children)
    .map(r => parseInt(r.getAttribute('id'), 10) || 0));

  const importes = new Map();   // "chemin|idSource" -> nouvel id dans le modele principal
  const partiesImportees = new Set();

  for (const composant of composants) {
    const partie = composant.getAttributeNS(NS_PROD, 'path').replace(/^\//, '');
    const idSource = composant.getAttribute('objectid');
    const cle = partie + '|' + idSource;

    if (!importes.has(cle)) {
      if (!fichiers[partie]) throw new Error('Partie 3MF introuvable : ' + partie);
      const objetSource = Array.from(lireXml(fichiers[partie]).getElementsByTagNameNS(NS_CORE, 'object'))
        .find(o => o.getAttribute('id') === idSource);
      if (!objetSource) throw new Error('Objet ' + idSource + ' introuvable dans ' + partie);

      const copie = doc.importNode(objetSource, true);
      const nouvelId = String(prochainId++);
      copie.setAttribute('id', nouvelId);
      for (const el of [copie, ...copie.getElementsByTagNameNS(NS_CORE, 'triangle')]) {
        ATTRIBUTS_MATERIAU.forEach(a => el.removeAttribute(a));
      }
      ressources.appendChild(copie);
      importes.set(cle, nouvelId);
      partiesImportees.add(partie);
    }

    composant.setAttribute('objectid', importes.get(cle));
    composant.removeAttributeNS(NS_PROD, 'path');
  }

  // Les parties importees ne doivent plus etre lues une seconde fois par le loader.
  partiesImportees.forEach(p => delete fichiers[p]);
  fichiers[chemin] = new TextEncoder().encode(new XMLSerializer().serializeToString(doc));

  const zip = zipSync(fichiers);
  return zip.buffer.slice(zip.byteOffset, zip.byteOffset + zip.byteLength);
}
