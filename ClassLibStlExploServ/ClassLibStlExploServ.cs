namespace ClassLibStlExploServ
{
    public class Packet
    {
        public int PacketID { get; set; }
        public string Description { get; set; } // Nom du dossier
        public int SujetID { get; set; }
        public Sujet Sujet { get; set; }
    }

    public class Sujet
    {
        public int SujetID { get; set; }
        public string NomSujet { get; set; }
        public int FamilleID { get; set; }
        public Famille Famille { get; set; }
    }

    public class Famille
    {
        public int FamilleID { get; set; }
        public string NomFamille { get; set; }
    }
}
