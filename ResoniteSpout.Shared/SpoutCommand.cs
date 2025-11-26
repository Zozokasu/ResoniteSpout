using Renderite.Shared;

namespace ResoniteSpout.Shared
{
    public enum SpoutCommandType
    {
        Create,
        Delete,
        Update,
        // Receiver commands
        ReceiverCreate,
        ReceiverDelete,
        ReceiverUpdate,
    }

    public class SpoutCommand : RendererCommand
    {
        public SpoutCommandType Type;
        public string SpoutName = "";
        public int AssetId;

        public override void Pack(ref MemoryPacker packer)
        {
            packer.Write(Type);
            packer.Write(AssetId);
            packer.Write(SpoutName);
        }

        public override void Unpack(ref MemoryUnpacker unpacker)
        {
            unpacker.Read(ref Type);
            unpacker.Read(ref AssetId);
            unpacker.Read(ref SpoutName);
        }
    }
}