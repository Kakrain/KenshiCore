using KenshiCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.OgreEngineering
{
    public enum MeshChunkType : ushort
    {
        MESH = 0x3000,
        GEOMETRY = 0x5000,
        SUBMESH = 0x4000,
        MESH_SKELETON_LINK = 0x6000,
        M_MESH_BONE_ASSIGNMENT = 0x7000,
        M_MESH_LOD_LEVEL = 0x8000,
        M_MESH_BOUNDS = 0x9000,
        M_SUBMESH_NAME_TABLE = 0xA000,
        M_EDGE_LISTS = 0xB000,
        M_POSES = 0xC000,
        M_ANIMATIONS = 0xD000,
        M_TABLE_EXTREMES = 0xE000,
        M_GEOMETRY_VERTEX_DECLARATION = 0x5100,
        M_GEOMETRY_VERTEX_BUFFER = 0x5200,
        M_GEOMETRY_VERTEX_ELEMENT = 0x5110,
        M_SUBMESH_BONE_ASSIGNMENT = 0x4100,
        M_SUBMESH_OPERATION = 0x4010,
        M_SUBMESH_TEXTURE_ALIAS = 0x4200,
        M_MESH_SKELETON_LINK = 0x6000,
        M_MESH_LOD_MANUAL = 0x8110,
        M_MESH_LOD_GENERATED = 0x8120,
        M_SUBMESH_NAME_TABLE_ELEMENT = 0xA100,
        M_EDGE_LIST_LOD = 0xB100,
        M_EDGE_GROUP = 0xB110,
        M_POSE = 0xC100,
        M_POSE_VERTEX = 0xC111,
        M_ANIMATION = 0xD100,
        M_ANIMATION_BASEINFO = 0xD105,
        M_ANIMATION_TRACK = 0xD110,
        M_ANIMATION_MORPH_KEYFRAME = 0xD111,
        M_ANIMATION_POSE_KEYFRAME = 0xD112,
        M_ANIMATION_POSE_REF = 0xD113
    }
    public class MeshEngineer
    {
        public MeshEngineer() { }

        private OgreContext? context;
        private string Name = "";
        private string version = "";
        private MeshReader meshreader = new FullMeshReader();
        private void ParseHeader(OgreContext ctx)
        {
            ctx.loadFlipEndian();
            version = ctx.ReadString();
            //CoreUtils.Print($"Version: {version}");
        }

        public void LoadMeshFile(string path)
        {
            meshreader = new FullMeshReader();
            using var fs = File.OpenRead(path);
            var ctx = new OgreContext(new BinaryReader(fs, Encoding.UTF8));
            context = ctx;
            string fileName = Path.GetFileName(path);
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            this.Name = fileName;

            //Logger.Print($"Loading mesh file: {fileName}");
            ParseHeader(ctx);

            meshreader.Read(ctx);
        }
        public string getSkeletonLink()
        {
            return meshreader.getSkeletonLink();
        }   
    }
    public abstract class MeshReader
    {
        public abstract string getSkeletonLink();
        public abstract void Read(OgreContext context);
        public MeshReader() { }

    }
    public class LightMeshReader : MeshReader
    {
        private List<string> skeleton_links = new();
        public override string getSkeletonLink()
        {

            if (skeleton_links.Count == 0)
                return "E_NOTFOUND";

            return string.Join(", ", skeleton_links);
        }

        public override void Read(OgreContext context)
        {
            var (id, length) = context.ReadChunkHeader();

            while (!context.IsEndOfStream(6))
            {
                LightMesh? chunk = null;
                switch (id)
                {
                    case (int)MeshChunkType.MESH:
                        chunk = new LightMesh(context);
                        break;
                }
                if (chunk != null)
                {
                    chunk.Read();
                    skeleton_links = chunk.getSkeletonLinks();
                }
                if (!context.IsEndOfStream(6))//this is necesary because Ogre can read less if necesary.
                    (id, length) = context.ReadChunkHeader();
            }
        }
    }
    public class FullMeshReader : MeshReader
    {
        private List<MeshChunk> chunks = new();
        public override string getSkeletonLink()
        {
            var names = chunks.OfType<SkeletonLink>().Select(x => x.getName()).ToList();

            if (names.Count == 0)
                return "E_NOTFOUND";

            return string.Join(", ", names);
        }

        public override void Read(OgreContext context)
        {
            var (id, length) = context.ReadChunkHeader();

            while (!context.IsEndOfStream(6))
            {
                MeshChunk? chunk = null;
                switch (id)
                {
                    case (int)MeshChunkType.MESH:
                        chunk = new Mesh(context);
                        break;
                }
                if (chunk != null)
                {
                    chunks.Add(chunk);
                    chunks.AddRange(chunk.Read());
                }
                if (!context.IsEndOfStream(6))//this is necesary because Ogre can read less if necesary.
                    (id, length) = context.ReadChunkHeader();
            }
        }
    }
}
