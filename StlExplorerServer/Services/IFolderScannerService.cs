namespace StlExplorerServer.Services
{
    public interface IFolderScannerService
    {
        void ScanFolder(string path);
        void ScanAllConfiguredFolders(); // <-- Nouvelle méthode ajoutée au contrat
    }
}
