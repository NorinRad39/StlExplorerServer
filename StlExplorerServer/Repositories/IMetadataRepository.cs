using Org.BouncyCastle.Asn1.Cms;
using ClassLibStlExploServ;


    namespace StlExplorerServer.Repositories
    {
        /// <summary>
        /// Interface pour le référentiel de métadonnées.
        /// </summary>
        public interface IMetadataRepository
        {
            /// <summary>
            /// Enregistre les métadonnées d'un paquet.
            /// </summary>
            /// <param name="packet">Le paquet de métadonnées à enregistrer.</param>
            void SaveMetadata(Packet packet);

            /// <summary>
            /// Récupère un sujet par son nom.
            /// </summary>
            /// <param name="name">Le nom du sujet à rechercher.</param>
            /// <returns>Le sujet correspondant, ou null s'il n'existe pas.</returns>
            Sujet GetSujetByName(string name);

            /// <summary>
            /// Enregistre un nouveau sujet.
            /// </summary>
            /// <param name="sujet">Le sujet à enregistrer.</param>
            void SaveSujet(Sujet sujet);

            /// <summary>
            /// Récupère une famille par son nom.
            /// </summary>
            /// <param name="name">Le nom de la famille à rechercher.</param>
            /// <returns>La famille correspondante, ou null si elle n'existe pas.</returns>
            Famille GetFamilleByName(string name);

            /// <summary>
            /// Enregistre une nouvelle famille.
            /// </summary>
            /// <param name="famille">La famille à enregistrer.</param>
            void SaveFamille(Famille famille);
        }
    }


