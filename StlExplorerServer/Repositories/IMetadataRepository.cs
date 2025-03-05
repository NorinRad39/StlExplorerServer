using Org.BouncyCastle.Asn1.Cms;
using ClassLibStlExploServ;

namespace StlExplorerServer.Repositories
{
    public interface IMetadataRepository
    {
        void SaveMetadata(Packet packet);
    }
}
