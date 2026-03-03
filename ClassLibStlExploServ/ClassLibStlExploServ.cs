namespace ClassLibStlExploServ
{
    /// <summary>
    /// Représente un modèle contenant des informations associées à un sujet.
    /// </summary>
    public class Modele
    {
        /// <summary>
        /// Identifiant unique du modèle.
        /// </summary>
        public int ModeleID { get; set; }

        /// <summary>
        /// Description du modèle.
        /// Peut être null.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Identifiant du sujet auquel ce modèle est associé.
        /// </summary>
        public int SujetID { get; set; }

        /// <summary>
        /// Référence au sujet associé.
        /// Peut être null.
        /// </summary>
        public Sujet? Sujet { get; set; }
    }

    /// <summary>
    /// Représente un sujet contenant plusieurs modèles et appartenant à une famille.
    /// </summary>
    public class Sujet
    {
        /// <summary>
        /// Identifiant unique du sujet.
        /// </summary>
        public int SujetID { get; set; }

        /// <summary>
        /// Nom du sujet.
        /// Peut être null.
        /// </summary>
        public string? NomSujet { get; set; }

        /// <summary>
        /// Identifiant de la famille à laquelle ce sujet appartient.
        /// </summary>
        public int FamilleID { get; set; }

        /// <summary>
        /// Référence à la famille associée.
        /// Peut être null.
        /// </summary>
        public Famille? Famille { get; set; }

        /// <summary>
        /// Collection de modèles liés à ce sujet.
        /// </summary>
        public ICollection<Modele> Modeles { get; set; } = new List<Modele>();
    }

    /// <summary>
    /// Représente une famille de sujets.
    /// </summary>
    public class Famille
    {
        /// <summary>
        /// Identifiant unique de la famille.
        /// </summary>
        public int FamilleID { get; set; }

        /// <summary>
        /// Nom de la famille.
        /// Peut être null.
        /// </summary>
        public string? NomFamille { get; set; }

        /// <summary>
        /// Collection de sujets appartenant à cette famille.
        /// </summary>
        public ICollection<Sujet> Sujets { get; set; } = new List<Sujet>();
    }
}
