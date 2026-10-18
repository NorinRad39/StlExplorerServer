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
// Performances : les parties externes pesent des dizaines de Mo de XML. Elles
// sont donc manipulees comme du texte (aucun DOM, aucune re-serialisation) et
// le zip renvoye n'est pas recompresse (niveau 0) : le loader le relit aussitot.
//
// Limite assumee : les materiaux (pid/pindex) des objets externes sont ignores,
// leurs identifiants renvoient a des ressources du fichier d'origine.
// ------------------------------------------------------------------
import { unzipSync, zipSync } from './vendor/three/addons/libs/fflate.module.js';

const decoder = new TextDecoder();
const encoder = new TextEncoder();

// Chemin du modele principal, declare dans _rels/.rels (3D/3dmodel.model par defaut).
function cheminModelePrincipal(fichiers) {
  const rels = fichiers['_rels/.rels'];
  if (rels) {
    const relations = decoder.decode(rels).match(/<Relationship\b[^>]*>/g) || [];
    for (const relation of relations) {
      if (/\bType="[^"]*3dmodel"/i.test(relation)) {
        const cible = relation.match(/\bTarget="([^"]*)"/);
        if (cible) return cible[1].replace(/^\//, '');
      }
    }
  }
  return '3D/3dmodel.model';
}

// Extrait le texte de <object id="idSource" ...>...</object> (les objets ne s'imbriquent pas).
function extraireObjet(xml, idSource, partie) {
  const debut = new RegExp('<object\\b[^>]*\\bid="' + idSource + '"[^>]*>').exec(xml);
  if (!debut) throw new Error('Objet ' + idSource + ' introuvable dans ' + partie);
  const fin = xml.indexOf('</object>', debut.index);
  if (fin < 0) throw new Error('Objet ' + idSource + ' mal forme dans ' + partie);
  return { balise: debut[0], texte: xml.slice(debut.index, fin + '</object>'.length) };
}

export function aplatir3mf(buffer) {
  const fichiers = unzipSync(new Uint8Array(buffer));
  const chemin = cheminModelePrincipal(fichiers);
  if (!fichiers[chemin]) return buffer;

  let principal = decoder.decode(fichiers[chemin]);
  if (!/\bp:path="/.test(principal)) return buffer;

  let prochainId = 1 + Math.max(0, ...Array.from(principal.matchAll(/(?<![\w:])id="(\d+)"/g), m => +m[1]));
  const importes = new Map();   // "partie|idSource" -> nouvel id dans le modele principal
  const parties = new Map();    // partie -> texte XML (decode une seule fois)
  const objetsAjoutes = [];

  principal = principal.replace(/<component\b[^>]*>/g, function (balise) {
    const chemin3mf = balise.match(/\bp:path="([^"]*)"/);
    if (!chemin3mf) return balise;

    const partie = chemin3mf[1].replace(/^\//, '');
    const idSource = (balise.match(/\bobjectid="([^"]*)"/) || [])[1];
    const cle = partie + '|' + idSource;

    if (!importes.has(cle)) {
      if (!fichiers[partie]) throw new Error('Partie 3MF introuvable : ' + partie);
      if (!parties.has(partie)) parties.set(partie, decoder.decode(fichiers[partie]));

      const objet = extraireObjet(parties.get(partie), idSource, partie);
      const nouvelId = String(prochainId++);
      let texte = objet.texte
        .replace(objet.balise, objet.balise.replace(/\bid="[^"]*"/, 'id="' + nouvelId + '"'));
      if (/ (pid|pindex|p1|p2|p3)="/.test(texte)) {
        texte = texte.replace(/ (?:pid|pindex|p1|p2|p3)="[^"]*"/g, '');
      }
      objetsAjoutes.push(texte);
      importes.set(cle, nouvelId);
    }

    return balise
      .replace(/\s+p:path="[^"]*"/, '')
      .replace(/\bobjectid="[^"]*"/, 'objectid="' + importes.get(cle) + '"');
  });

  const finRessources = principal.lastIndexOf('</resources>');
  if (finRessources < 0) throw new Error('3MF sans section <resources>');
  principal = principal.slice(0, finRessources) + objetsAjoutes.join('') + principal.slice(finRessources);

  // Les parties importees ne doivent plus etre lues une seconde fois par le loader.
  new Set(Array.from(importes.keys(), c => c.split('|')[0])).forEach(p => delete fichiers[p]);
  fichiers[chemin] = encoder.encode(principal);

  const zip = zipSync(fichiers, { level: 0 });
  return zip.buffer.slice(zip.byteOffset, zip.byteOffset + zip.byteLength);
}
