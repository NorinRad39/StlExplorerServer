namespace ClassLibStlExploServ
{
    #region Classe Modele

    /// <summary>
    /// Représente un modèle contenant des informations associées à un sujet.
    /// Il s'agit de l'entité la plus spécifique dans la hiérarchie.
    /// </summary>
    /// <remarks>
    /// Chaque <see cref="Modele"/> doit obligatoirement être rattaché à un <see cref="Sujet"/>.
    /// </remarks>
    /// <example>
    /// Voici comment créer une instance de cette classe :
    /// <code>
    /// var monModele = new Modele
    /// {
    ///     Description = "Fichier STL d'une roue avant",
    ///     SujetID = 1 // Doit correspondre à l'ID d'un sujet existant
    /// };
    /// </code>
    /// </example>
    public class Modele
    {
        #region Propriétés Principales

        /// <summary>
        /// Identifiant unique du modèle (Clé primaire dans la base de données).
        /// Il est généralement généré automatiquement lors de l'insertion.
        /// </summary>
        public int ModeleID { get; set; }

        /// <summary>
        /// Description du modèle (ex: nom du fichier, détails).
        /// Le point d'interrogation (?) indique que cette valeur peut être nulle.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Chemin du dossier où le fichier associé au modèle est stocké.
        /// Utile pour retrouver facilement les fichiers physiques sur le disque.
        /// </summary>
        public string? CheminDossier { get; set; }

        /// <summary>
        /// Liste des chemins vers les images (ex: jpg, png) trouvées dans le dossier du modèle.
        /// Utile pour afficher une galerie ou une image de couverture dans l'interface utilisateur.
        /// </summary>
        public List<string> CheminsImages { get; set; } = [];

        #endregion

        #region Propriétés de Navigation et Clés Étrangères

        /// <summary>
        /// Identifiant du sujet auquel ce modèle est associé (Clé étrangère).
        /// Permet de faire le lien avec la table des sujets dans la base de données.
        /// </summary>
        public int SujetID { get; set; }

        /// <summary>
        /// Propriété de navigation : Référence directe à l'objet <see cref="Sujet"/> associé.
        /// Cela permet d'accéder aux informations du sujet cible directement depuis le modèle (ex: <c>monModele.Sujet.NomSujet</c>).
        /// Peut être null si les données associées n'ont pas encore été chargées (Lazy Loading ou Include manquant).
        /// </summary>
        public Sujet? Sujet { get; set; }

        #endregion
    }

    #endregion

    #region Classe Sujet

    /// <summary>
    /// Représente un sujet contenant plusieurs modèles et appartenant à une famille.
    /// C'est le niveau intermédiaire de la hiérarchie.
    /// </summary>
    /// <remarks>
    /// Un <see cref="Sujet"/> regroupe généralement plusieurs <see cref="Modele"/> et appartient à une seule <see cref="Famille"/>.
    /// </remarks>
    /// <example>
    /// Voici comment instancier un sujet et y ajouter un modèle dans la foulée :
    /// <code>
    /// var monSujet = new Sujet
    /// {
    ///     NomSujet = "Voiture de course",
    ///     FamilleID = 2
    /// };
    /// monSujet.Modeles.Add(new Modele { Description = "Chassis.stl" });
    /// </code>
    /// </example>
    public class Sujet
    {
        #region Propriétés Principales

        /// <summary>
        /// Identifiant unique du sujet (Clé primaire dans la base de données).
        /// </summary>
        public int SujetID { get; set; }

        /// <summary>
        /// Nom donné au sujet.
        /// Le type d'ancrage "string?" signifie que la chaîne de caractères est nullable.
        /// </summary>
        public string? NomSujet { get; set; }

        #endregion

        #region Propriétés de Navigation et Clés Étrangères

        /// <summary>
        /// Identifiant de la famille à laquelle ce sujet appartient (Clé étrangère vers Famille).
        /// </summary>
        public int FamilleID { get; set; }

        /// <summary>
        /// Propriété de navigation : Référence directe à la <see cref="Famille"/> associée.
        /// </summary>
        public Famille? Famille { get; set; }

        /// <summary>
        /// Collection (liste) des modèles liés à ce sujet.
        /// Elle est initialisée par défaut avec une liste vide pour éviter les erreurs de type "NullReferenceException"
        /// lors de l'ajout d'un nouvel élément avec <c>Modeles.Add(...)</c>.
        /// </summary>
        public ICollection<Modele> Modeles { get; set; } = [];

        #endregion
    }

    #endregion

    #region Classe Famille

    /// <summary>
    /// Représente une famille qui regroupe un ensemble de sujets.
    /// C'est le niveau le plus haut dans cette hiérarchie de données.
    /// </summary>
    /// <remarks>
    /// Cette classe sert souvent de catégorie principale.
    /// </remarks>
    /// <example>
    /// Exemple de création d'une famille complète :
    /// <code>
    /// var maFamille = new Famille
    /// {
    ///     NomFamille = "Véhicules"
    /// };
    /// maFamille.Sujets.Add(new Sujet { NomSujet = "Avion" });
    /// maFamille.Sujets.Add(new Sujet { NomSujet = "Bateau" });
    /// </code>
    /// </example>
    public class Famille
    {
        #region Propriétés Principales

        /// <summary>
        /// Identifiant unique de la famille (Clé primaire).
        /// </summary>
        public int FamilleID { get; set; }

        /// <summary>
        /// Nom de la famille.
        /// Peut être null.
        /// </summary>
        public string? NomFamille { get; set; }

        #endregion

        #region Propriétés de Navigation

        /// <summary>
        /// Collection des sujets appartenant à cette famille.
        /// Initialisée avec une nouvelle liste pour pouvoir y ajouter facilement des éléments.
        /// Une <see cref="ICollection{T}"/> est l'interface standard utilisée par Entity Framework pour les relations "Un-à-Plusieurs" (One-to-Many).
        /// </summary>
        public ICollection<Sujet> Sujets { get; set; } = [];

        #endregion
    }

    #endregion

    #region Classe CreerModeleRequete

    /// <summary>
    /// DTO (Data Transfer Object) pour la requête de création d'un nouveau modèle.
    /// Contient les noms des 3 niveaux de la hiérarchie (Famille > Sujet > Modèle).
    /// </summary>
    public class CreerModeleRequete
    {
        /// <summary>Nom de la famille (niveau 1).</summary>
        public string NomFamille { get; set; } = "";

        /// <summary>Nom du sujet (niveau 2).</summary>
        public string NomSujet { get; set; } = "";

        /// <summary>Nom du modèle (niveau 3).</summary>
        public string NomModele { get; set; } = "";
    }

    #endregion

    #region Classe ModeleResume

    /// <summary>
    /// DTO léger pour le chargement rapide des listes déroulantes.
    /// Contient uniquement les noms et identifiants, sans les chemins d'images.
    /// </summary>
    public class ModeleResume
    {
        public int ModeleID { get; set; }
        public string? Description { get; set; }
        public string? NomSujet { get; set; }
        public int SujetID { get; set; }
        public string? NomFamille { get; set; }
        public int FamilleID { get; set; }
    }

    #endregion
}
