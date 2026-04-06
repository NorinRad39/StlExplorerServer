namespace StlExplorerServer.Services
{
    public interface IFolderScannerService
    {
        void ScanFolder(string path);
        void ScanAllConfiguredFolders();
        void ActualiserBaseDepuisDossier(string path);
        void SynchronisationIntelligente();
        void InvaliderCache(string? path = null);
    }
}
