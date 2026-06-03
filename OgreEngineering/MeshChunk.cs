using KenshiCore.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.DataFormats;

namespace KenshiCore.OgreEngineering
{
    public abstract class MeshChunk
    {
        protected OgreContext context;
        protected List<MeshChunk> chunks = new();
        public abstract List<MeshChunk> Read();
        protected MeshChunk(OgreContext context)
        {
            this.context = context;
        }
    }

    public class LightMesh
    {
        private List<string> skeleton_links = new();
        private OgreContext context;
        public LightMesh(OgreContext context) {
            this.context = context;
        }
        public List<string> getSkeletonLinks()
        {
            return skeleton_links;
        }
        public void Read()
        {
            bool skeletallyAnimated = context.ReadBool();
            if (!context.IsEndOfStream(0))
            {
                var (id, length) = context.ReadChunkHeader();

                while (!context.IsEndOfStream(0) && (id == (int)MeshChunkType.GEOMETRY || id == (int)MeshChunkType.SUBMESH || id == (int)MeshChunkType.MESH_SKELETON_LINK
                    || id == (int)MeshChunkType.M_MESH_BONE_ASSIGNMENT || id == (int)MeshChunkType.M_MESH_LOD_LEVEL || id == (int)MeshChunkType.M_MESH_BOUNDS
                    || id == (int)MeshChunkType.M_SUBMESH_NAME_TABLE || id == (int)MeshChunkType.M_EDGE_LISTS || id == (int)MeshChunkType.M_POSES
                    || id == (int)MeshChunkType.M_ANIMATIONS || id == (int)MeshChunkType.M_TABLE_EXTREMES))
                {
                    SkeletonLink? chunk = null;
                    //Logger.Print($"Mesh chunk ID: 0x{id:X4}");
                    switch (id)
                    {
                        case (int)MeshChunkType.GEOMETRY:
                            context.offsetStream((int)length);
                            break;
                        case (int)MeshChunkType.SUBMESH:
                            context.offsetStream((int)length);
                            break;
                        case (int)MeshChunkType.MESH_SKELETON_LINK:
                            chunk = new SkeletonLink(context);
                            break;
                        case (int)MeshChunkType.M_MESH_BONE_ASSIGNMENT:
                            context.offsetStream((int)length);
                            break;
                        case (int)MeshChunkType.M_MESH_LOD_LEVEL:
                            context.offsetStream((int)length);
                            break;
                        case (int)MeshChunkType.M_MESH_BOUNDS:
                            context.offsetStream((int)length);
                            break;
                        case (int)MeshChunkType.M_SUBMESH_NAME_TABLE:
                            context.offsetStream((int)length);
                            break;
                        case (int)MeshChunkType.M_EDGE_LISTS:
                            context.offsetStream((int)length);
                            break;
                        case (int)MeshChunkType.M_POSES:
                            context.offsetStream((int)length);
                            break;
                        case (int)MeshChunkType.M_ANIMATIONS:
                            context.offsetStream((int)length);
                            break;
                        case (int)MeshChunkType.M_TABLE_EXTREMES:
                            context.offsetStream((int)length);
                            break;
                    }
                    if (chunk != null)
                    {
                        chunk.Read();
                        skeleton_links.Add(chunk.getName());
                    }
                    if (!context.IsEndOfStream(0))
                        (id, length) = context.ReadChunkHeader();
                }
                if (!context.IsEndOfStream(0))
                {
                    context.offsetStream(-6);
                }
            }
        }
    }

    public class Mesh : MeshChunk
    {
        public Mesh(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            bool skeletallyAnimated = context.ReadBool();
            Logger.Print($"Skeletally Animated: {skeletallyAnimated}");
            if (!context.IsEndOfStream(0))
            {
                var (id, length) = context.ReadChunkHeader();
                Logger.Print($"Mesh INICIAL chunk ID: 0x{id:X4}");

                while (!context.IsEndOfStream(0) && (id == (int)MeshChunkType.GEOMETRY || id == (int)MeshChunkType.SUBMESH || id == (int)MeshChunkType.MESH_SKELETON_LINK 
                    || id == (int)MeshChunkType.M_MESH_BONE_ASSIGNMENT || id == (int)MeshChunkType.M_MESH_LOD_LEVEL || id == (int)MeshChunkType.M_MESH_BOUNDS 
                    || id == (int)MeshChunkType.M_SUBMESH_NAME_TABLE || id == (int)MeshChunkType.M_EDGE_LISTS || id == (int)MeshChunkType.M_POSES
                    || id == (int)MeshChunkType.M_ANIMATIONS|| id == (int)MeshChunkType.M_TABLE_EXTREMES))
                {
                    MeshChunk? chunk = null;
                    Logger.Print($"Mesh chunk ID: 0x{id:X4}");
                    switch (id)
                    {
                        case (int)MeshChunkType.GEOMETRY:
                            chunk = new Geometry(context);
                            break;
                        case (int)MeshChunkType.SUBMESH:
                            chunk = new SubMesh(context);
                            break;
                        case (int)MeshChunkType.MESH_SKELETON_LINK:
                            chunk = new SkeletonLink(context);
                            break;
                        case (int)MeshChunkType.M_MESH_BONE_ASSIGNMENT:
                            chunk = new MeshBoneAssignment(context);
                            break;
                        case (int)MeshChunkType.M_MESH_LOD_LEVEL:
                            context.offsetStream((int)length);
                            Logger.Print($"Skipping M_MESH_LOD_LEVEL chunk with length {length} bytes");
                            //chunk = new MeshLodLevel(context);
                            //((MeshLodLevel)chunk).setNumSubs((ushort)chunks.Count(c => c is SubMesh));
                            break;
                        case (int)MeshChunkType.M_MESH_BOUNDS:
                            chunk = new MeshBounds(context);
                            break;
                        case (int)MeshChunkType.M_SUBMESH_NAME_TABLE:
                            chunk = new SubMeshNameTable(context);
                            break;
                        case (int)MeshChunkType.M_EDGE_LISTS:
                            chunk = new EdgeLists(context);
                            break;
                        case (int)MeshChunkType.M_POSES:
                            chunk = new Poses(context);
                            break;
                        case (int)MeshChunkType.M_ANIMATIONS:
                            chunk = new Animations(context);
                            break;
                        case (int)MeshChunkType.M_TABLE_EXTREMES:
                            chunk = new Extremes(context);
                            ((Extremes)chunk).setCurrentStreamLen(length);
                            break;
                    }
                    if (chunk != null)
                    {
                        chunks.Add(chunk);
                        chunks.AddRange(chunk.Read());
                    }
                    if (!context.IsEndOfStream(0))
                        (id, length) = context.ReadChunkHeader();
                }
                if (!context.IsEndOfStream(0))
                {
                    context.offsetStream(-6);
                }
            }
            return chunks;
        }
    }
    public class Geometry : MeshChunk
    {
        public Geometry(OgreContext context) : base(context) { }
        private uint vertexCount = 0;
        public uint getVertexCount()
        {
            return vertexCount;
        }
        public override List<MeshChunk> Read()
        {
            Logger.Print("reading Geometry");
            vertexCount = context.ReadUInt32();
            Logger.Print($"Vertex Count: {vertexCount}");
            var (id, length) = context.ReadChunkHeader();
            Logger.Print($"Geometry chunk ID: 0x{id:X4}");

            while (id == (int)MeshChunkType.M_GEOMETRY_VERTEX_DECLARATION || id == (int)MeshChunkType.M_GEOMETRY_VERTEX_BUFFER)
            {
                MeshChunk? chunk = null;
                switch (id)
                {
                    case (int)MeshChunkType.M_GEOMETRY_VERTEX_DECLARATION:
                        chunk = new GeometryVertexDeclaration(context);
                        break;
                    case (int)MeshChunkType.M_GEOMETRY_VERTEX_BUFFER:
                        chunk = new GeometryVertexBuffer(context);
                        ((GeometryVertexBuffer)chunk).setVertexCount(vertexCount);
                        break;
                }
                if (chunk != null)
                {
                    chunks.Add(chunk);
                    chunks.AddRange(chunk.Read());
                }
                (id, length) = context.ReadChunkHeader();
            }
            if (!context.IsEndOfStream(6))
            {
                context.offsetStream(-6);
            }
            return chunks;
        }
    }
    public class GeometryVertexDeclaration : MeshChunk
    {
        public GeometryVertexDeclaration(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            var (id, length) = context.ReadChunkHeader();
            Logger.Print($"GeometryVertexDeclaration id {id} length {length}");
            while (id == (int)MeshChunkType.M_GEOMETRY_VERTEX_ELEMENT)
            {
                var chunk = new GeometryVertexElement(context);


                chunk.Read();
                if (!context.IsEndOfStream(6))
                    (id, length) = context.ReadChunkHeader();
            }
            context.offsetStream(-6);
            return chunks;
        }
    }
    public class GeometryVertexBuffer : MeshChunk
    {
        uint vertexCount = 0;
        public GeometryVertexBuffer(OgreContext context) : base(context) { }
        public void setVertexCount(uint vertexCount)
        {
            this.vertexCount = vertexCount;
        }
        public override List<MeshChunk> Read()
        {
            ushort bindIndex = context.ReadUInt16();
            ushort vertexSize = context.ReadUInt16();
            var (id, length) = context.ReadChunkHeader();
            context.ReadBytes(vertexSize * (int)vertexCount);
            Logger.Print($"bindIndex {bindIndex}, vertexSize {vertexSize}, readbytes count{vertexSize * (int)vertexCount}");
            return chunks;
        }
    }
    public class GeometryVertexElement : MeshChunk
    {
        public GeometryVertexElement(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            ushort source = context.ReadUInt16();
            ushort tmp = context.ReadUInt16();
            tmp = context.ReadUInt16();//yes second tmp is necesary
            ushort offset = context.ReadUInt16();
            ushort index = context.ReadUInt16();
            Logger.Print($"source {source},tmp {tmp},offset {offset}, index {index}");
            return chunks;
        }
    }
    public class SubMeshOperation : MeshChunk
    {
        public SubMeshOperation(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            ushort optype = context.ReadUInt16();
            Logger.Print($"SubMeshOperation optype: {optype}");
            return chunks;
        }
    }
    public class SubMeshBoneAssignment : MeshChunk
    {
        public SubMeshBoneAssignment(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            uint vertexindex = context.ReadUInt32();
            ushort boneindex = context.ReadUInt16();
            float weight = context.ReadFloat();
            Logger.Print($"SubMeshBoneAssignment vertexindex: {vertexindex}, boneindex: {boneindex}, weight: {weight}");
            return chunks;
        }
    }
    public class MeshBoneAssignment : MeshChunk
    {
        public MeshBoneAssignment(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            uint vertexindex = context.ReadUInt32();
            ushort boneindex = context.ReadUInt16();
            float weight = context.ReadFloat();
            Logger.Print($"MeshBoneAssignment vertexindex: {vertexindex}, boneindex: {boneindex}, weight: {weight}");
            return chunks;
        }
    }
    public class MeshLodLevel : MeshChunk
    {
        ushort numSubs = 0;
        public void setNumSubs(ushort numSubs)
        {
            this.numSubs = numSubs;
        }
        public MeshLodLevel(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            /*string strategyName = context.ReadString();
            ushort numLods = context.ReadUInt16();
            for (int lodID = 1; lodID < numLods; lodID++)
            { 
                var(id, length) = context.ReadChunkHeader();
                float userValue = context.ReadFloat();
                MeshChunk? chunk = null;
                switch (id)
                {
                    case (int)MeshChunkType.M_MESH_LOD_MANUAL:
                        chunk = new MeshLodUsageManual(context);

                        break;
                    case (int)MeshChunkType.M_MESH_LOD_GENERATED:

                        chunk = new MeshLodUsageGenerated(context);
                        (chunk as MeshLodUsageGenerated)?.setNumSubMeshes(numSubs);
                        break;
                    default:
                        Logger.Print($"Unknown LOD level chunk ID: 0x{id:X4}");
                        break;

                }
                if(chunk != null)
                {
                    chunks.Add(chunk);
                    chunks.AddRange(chunk.Read());
                }
            }*/
                String strategyName = context.ReadString();
                ushort numLods = context.ReadUInt16();
                Logger.Print($"MeshLodLevel strategyName: {strategyName}, numLods: {numLods}");

                for (int lodID = 1; lodID < numLods; lodID++)
                {
                    var (id, length) = context.ReadChunkHeader();
                    float usageValue = context.ReadFloat();
                    Logger.Print($"LOD ID: {lodID}, chunk ID: 0x{id:X4}, usageValue: {usageValue}");
                    switch (id)
                    {
                        case (int)MeshChunkType.M_MESH_LOD_MANUAL:
                            String name = context.ReadString();
                            Logger.Print($"M_MESH_LOD_MANUAL name: {name}");
                            break;
                        case (int)MeshChunkType.M_MESH_LOD_GENERATED:
                            for (int i = 0; i < numSubs; ++i)
                            {
                                uint numIndexes = context.ReadUInt32();
                                uint offset = context.ReadUInt32();
                                uint bufferIndex = context.ReadUInt32();
                                Logger.Print($"M_MESH_LOD_GENERATED numIndexes: {numIndexes}, offset: {offset}, bufferIndex: {bufferIndex}");
                                if (bufferIndex == unchecked((uint)-1))
                                {
                                    bool idx32Bit=context.ReadBool();
                                    uint buffIndexCount = context.ReadUInt32();
                                    context.offsetStream((int)buffIndexCount * (idx32Bit ? 4 : 2));
                                    Logger.Print($"M_MESH_LOD_GENERATED has index buffer. idx32Bit: {idx32Bit}, buffIndexCount: {buffIndexCount}");
                                }
                            }
                            break;
                        default:
                            Logger.Print($"Unknown LOD level chunk ID: 0x{id:X4}");
                            break;
                    }
                }
                
                return chunks;
        }
    }
    public class SkeletonLink : MeshChunk
    {
        public SkeletonLink(OgreContext context) : base(context) { }
        private string name = "";
        public string getName()
        {
            return name;
        }
        public override List<MeshChunk> Read()
        {
            name = context.ReadString();
            //Logger.Print($"SkeletonLink name: {name}");
            return chunks;
        }
    }
    public class MeshLodUsageManual : MeshChunk
    {
        public MeshLodUsageManual(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            string manualName = context.ReadString();
            return chunks;
        }
    }
    public class MeshLodUsageGenerated : MeshChunk
    {
        public MeshLodUsageGenerated(OgreContext context) : base(context) { }
        private int numSubMeshes = 0;
        public void setNumSubMeshes(int numSubMeshes)
        {
            this.numSubMeshes = numSubMeshes;
        }
        public override List<MeshChunk> Read()
        {
            string manualName = context.ReadString();
            for (int i = 0; i < numSubMeshes; i++)
            {
                uint numIndexes = context.ReadUInt32();
                uint bufferIndex = context.ReadUInt32();
                if (bufferIndex == unchecked((uint)-1))
                {
                    bool idx32Bit = context.ReadBool();
                    uint buffIndexCount = context.ReadUInt32();
                    if (idx32Bit)
                    {
                        uint[] data = context.ReadUInts32((int)buffIndexCount);
                    }
                    else
                    {
                        ushort[] data = context.ReadUInts16((int)buffIndexCount);
                    }
                }
            }
            Logger.Print($"MeshLodUsageGenerated manualName: {manualName}, numSubMeshes: {numSubMeshes}");
            return chunks;
        }
    }

    public class MeshBounds : MeshChunk
    {
        public MeshBounds(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            float[] min = context.ReadFloats(3);
            float[] max = context.ReadFloats(3);
            float radius = context.ReadFloat();
            Logger.Print($"min : {string.Join(",", min)}");
            Logger.Print($"max : {string.Join(",", max)}");
            Logger.Print($"radius : {radius}");
            return chunks;
        }
    }
    public class SubMeshNameTable : MeshChunk
    {
        public SubMeshNameTable(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            Logger.Print("Reading SubMeshNameTable");
            if (!context.IsEndOfStream())
            {
                var (id, length) = context.ReadChunkHeader();
                Logger.Print($"SubMeshNameTable chunk ID: 0x{id:X4}");
                while (!context.IsEndOfStream() && (id == (int)MeshChunkType.M_SUBMESH_NAME_TABLE_ELEMENT))
                {
                    ushort subMeshIndex = context.ReadUInt16();
                    string currentName = context.ReadString();
                    if (!context.IsEndOfStream())
                        (id, length) = context.ReadChunkHeader();
                    Logger.Print($"SubMeshNameTableElement subMeshIndex: {subMeshIndex}, currentName: {currentName}");
                }
                if (!context.IsEndOfStream())
                {
                    context.offsetStream(-6);
                }
            }
            return chunks;
        }
    }
    public class EdgeListLodInfo : MeshChunk
    {
        private bool hasEdgeData = false;
        public void setHasEdgeData(bool hasEdgeData)
        {
            this.hasEdgeData = hasEdgeData;
        }
        public EdgeListLodInfo(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            bool isClosed;
            uint numTriangles;
            uint numEdgeGroups;
            if (!hasEdgeData)
            {
                isClosed = context.ReadBool();
                numTriangles = context.ReadUInt32();
                numEdgeGroups = context.ReadUInt32();
                context.offsetStream((int)(numTriangles * 48));

                for (uint eg = 0; eg < numEdgeGroups; ++eg)
                {
                    var (id, length) = context.ReadChunkHeader();
                    if (id != (int)MeshChunkType.M_EDGE_GROUP)
                    {
                        throw new Exception($"Expected M_EDGE_GROUP chunk, but got 0x{id:X4}");
                    }
                    uint[] tmp = context.ReadUInts32(3);
                    uint numEdges = context.ReadUInt32();
                    context.offsetStream((int)(numEdges * 25));
                }
                Logger.Print($"EdgeListLodInfo has no edge data. isClosed: {isClosed}, numTriangles: {numTriangles}, numEdgeGroups: {numEdgeGroups}");
                return chunks;
            }

            isClosed = context.ReadBool();
            numTriangles = context.ReadUInt32();
            numEdgeGroups = context.ReadUInt32();


            for (int t = 0; t < numTriangles; ++t)
            {
                uint indexSet = context.ReadUInt32();
                uint vertexSet = context.ReadUInt32();
                uint[] vertIndex = context.ReadUInts32(3);
                uint[] sharedVertIndex = context.ReadUInts32(3);
                float[] triangleFaceNormals = context.ReadFloats(4);
            }
            for (uint eg = 0; eg < numEdgeGroups; ++eg)
            {
                var (id, length) = context.ReadChunkHeader();
                if (id != (int)MeshChunkType.M_EDGE_GROUP)
                {
                    throw new Exception($"Expected M_EDGE_GROUP chunk, but got 0x{id:X4}");
                }
                uint vertexSet = context.ReadUInt32();
                uint triStart = context.ReadUInt32();
                uint triCount = context.ReadUInt32();
                uint numEdges = context.ReadUInt32();
                for (uint e = 0; e < numEdges; ++e)
                {
                    uint[] triIndex = context.ReadUInts32(2);
                    uint[] vertIndex = context.ReadUInts32(2);
                    uint[] sharedVertIndex = context.ReadUInts32(2);
                    bool degenerate = context.ReadBool();
                }
            }
            Logger.Print($"EdgeListLodInfo isClosed: {isClosed}, numTriangles: {numTriangles}, numEdgeGroups: {numEdgeGroups}");
            return chunks;
        }
    }
        public class EdgeLists : MeshChunk
        {
            public EdgeLists(OgreContext context) : base(context) { }
            public override List<MeshChunk> Read()
            {   
            
            Logger.Print("Reading EdgeLists");
            if (!context.IsEndOfStream())
                {
                    var (id, length) = context.ReadChunkHeader();
                    while (!context.IsEndOfStream() && (id == (int)MeshChunkType.M_EDGE_LIST_LOD))
                    {
                        ushort lodIndex = context.ReadUInt16();
                        bool isManual = context.ReadBool();
                        if (!isManual)
                        {
                            var chunk = new EdgeListLodInfo(context);
                            chunk.setHasEdgeData(true);
                            chunks.Add(chunk);
                            chunks.AddRange(chunk.Read());
                        }
                        if (!context.IsEndOfStream())
                        {
                            (id, length) = context.ReadChunkHeader();
                        }
                    }
                    if (!context.IsEndOfStream())
                    {
                        context.offsetStream(-6);
                    }
                        
                }
                return chunks;
            }
        }
    public class Poses : MeshChunk
    {
        public Poses(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            Logger.Print("Reading Poses");
            if (!context.IsEndOfStream())
            {
                var(id, length) = context.ReadChunkHeader();
                while (!context.IsEndOfStream() && (id == (int)MeshChunkType.M_POSE))
                {
                    switch (id)
                    {
                        case (int)MeshChunkType.M_POSE:
                            var chunk = new Pose(context);
                            chunks.Add(chunk);
                            chunks.AddRange(chunk.Read());
                            break;
                    }
                    if(!context.IsEndOfStream())
                    {
                        (id, length) = context.ReadChunkHeader();
                    }

                }
                if (!context.IsEndOfStream())
                {
                    context.offsetStream(-6);
                }
            }
            return chunks;
        }
    }
    public class Animations : MeshChunk
    {
        public Animations(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            Logger.Print("Reading Animations");
            if (!context.IsEndOfStream())
            {
                var (id, length) = context.ReadChunkHeader();
                while (!context.IsEndOfStream() && (id == (int)MeshChunkType.M_ANIMATION))
                {
                    switch (id)
                    {
                        case (int)MeshChunkType.M_ANIMATION:
                            var chunk = new Animation(context);
                            chunks.Add(chunk);
                            chunks.AddRange(chunk.Read());
                            break;
                    }
                    if (!context.IsEndOfStream())
                    {
                        (id, length) = context.ReadChunkHeader();
                    }

                }
                if (!context.IsEndOfStream())
                {
                    context.offsetStream(-6);
                }
            }
            return chunks;
        }
    }
    public class Extremes : MeshChunk
    {
        uint mCurrentstreamLen = 0;
        public Extremes(OgreContext context) : base(context) { }
        public void setCurrentStreamLen(uint mCurrentstreamLen)
        {
            this.mCurrentstreamLen = mCurrentstreamLen;
        }
        public override List<MeshChunk> Read()
        {
            ushort idx=context.ReadUInt16();
            uint minChunkSize = 8;
            int n_floats= (int)((mCurrentstreamLen - minChunkSize) / 4);
            float[] extremity_points=new float[n_floats];
            Logger.Print($"Extremes idx: {idx}, mCurrentstreamLen: {mCurrentstreamLen}, n_floats: {n_floats}");
            return chunks;
        }
    }
    
    public class Pose : MeshChunk
    {
        public Pose(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {

            string name= context.ReadString();

            ushort target = context.ReadUInt16();

            bool includeNormals = context.ReadBool();
            Logger.Print($"Pose name: {name}, target: {target}, includeNormals: {includeNormals}");

            if (!context.IsEndOfStream())
            {
                var (id, length) = context.ReadChunkHeader();


                while (!context.IsEndOfStream() && (id == (int)MeshChunkType.M_POSE_VERTEX))
                {

                    switch (id)
                    {
                        case (int)MeshChunkType.M_POSE_VERTEX:
                            uint vertexIndex = context.ReadUInt32();

                            float[] offset=context.ReadFloats(3);
                            if (includeNormals)
                            {
                                float[] normals = context.ReadFloats(3);
                            }
                            Logger.Print($"Pose vertexIndex: {vertexIndex}, offset: {string.Join(",", offset)}");
                            break;
                    }
                    if(!context.IsEndOfStream())
                        (id, length) = context.ReadChunkHeader();
                }
                if (!context.IsEndOfStream())
                {
                    context.offsetStream(-6);
                }

            }
            return chunks;
        }
    }
    public class Animation : MeshChunk
    {
        public Animation(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            string name = context.ReadString();
            float len = context.ReadFloat();
            Logger.Print($"Animation name: {name}, length: {len}");
            if (!context.IsEndOfStream())
            {
                var (id, length) = context.ReadChunkHeader();
                if (id == (int)MeshChunkType.M_ANIMATION_BASEINFO)
                {
                    string baseAnimName = context.ReadString();
                    float baseKeyTime = context.ReadFloat();

                    if (!context.IsEndOfStream())
                        (id, length) = context.ReadChunkHeader();
                }
                while (!context.IsEndOfStream() && (id == (int)MeshChunkType.M_ANIMATION_TRACK))
                {
                    switch (id)
                    {
                        case (int)MeshChunkType.M_ANIMATION_TRACK:
                            MeshChunk chunk = new AnimationTrack(context);
                            chunks.Add(chunk);
                            chunks.AddRange(chunk.Read());
                            break;
                    }
                    if (!context.IsEndOfStream())
                        (id, length) = context.ReadChunkHeader();
                }
                if (!context.IsEndOfStream())
                {
                    context.offsetStream(-6);
                }
            }

            return chunks;
        }
    }
    public class AnimationTrack : MeshChunk
    {
        public AnimationTrack(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            ushort inAnimType=context.ReadUInt16();
            ushort target = context.ReadUInt16();
            Logger.Print($"AnimationTrack inAnimType: {inAnimType}, target: {target}");
            if (!context.IsEndOfStream())
            {
                MeshChunk? chunk = null;
                var (id, length) = context.ReadChunkHeader();
                while (!context.IsEndOfStream() && (id == (int)MeshChunkType.M_ANIMATION_MORPH_KEYFRAME || id == (int)MeshChunkType.M_ANIMATION_POSE_KEYFRAME))
                {
                    switch (id)
                    {
                        case (int)MeshChunkType.M_ANIMATION_MORPH_KEYFRAME:
                            chunk = new MorphKeyframe(context);
                            ((MorphKeyframe)chunk).setVertexCount((chunks.Find(c => c is Geometry) as Geometry)?.getVertexCount() ?? 0);
                            chunks.Add(chunk);
                            chunks.AddRange(chunk.Read());
                            break;
                        case (int)MeshChunkType.M_ANIMATION_POSE_KEYFRAME:
                            chunk = new PoseKeyframe(context);
                            chunks.Add(chunk);
                            chunks.AddRange(chunk.Read());
                            break;
                    }
                    if (!context.IsEndOfStream())
                    {
                        (id, length) = context.ReadChunkHeader();
                    }
                }
                if (!context.IsEndOfStream())
                {
                    context.offsetStream(-6);
                }
            }
            return chunks;
        }
    }
    public class MorphKeyframe : MeshChunk
    {
        public MorphKeyframe(OgreContext context) : base(context) { }
        private uint vertexCount = 0;
        public void setVertexCount(uint vertexCount)
        {
            this.vertexCount = vertexCount;
        }
        public override List<MeshChunk> Read()
        {
            Logger.Print("Reading PoseKeyframe");
            float timePos=context.ReadFloat();
            bool includeNormals = context.ReadBool();
            int vertexSize = 4 * (includeNormals ? 6 : 3);
            float[] data = context.ReadFloats((int)(vertexCount *(includeNormals ? 6 : 3)));
            Logger.Print($"Reading MorphKeyframe: timePos = {timePos}, includeNormals = {includeNormals}, vertexCount = {vertexCount}");
            return chunks;
        }
    }
    public class PoseKeyframe : MeshChunk
    {
        public PoseKeyframe(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            Logger.Print("Reading PoseKeyframe");
            float timePos = context.ReadFloat();
            if(!context.IsEndOfStream())
            {
                var (id, length) = context.ReadChunkHeader();
                while(!context.IsEndOfStream() && id == (int)MeshChunkType.M_ANIMATION_POSE_REF)
                {
                    switch (id)
                    {
                        case (int)MeshChunkType.M_ANIMATION_POSE_REF:
                            ushort poseIndex= context.ReadUInt16();
                            float influence= context.ReadFloat();
                            Logger.Print($"Reading M_ANIMATION_POSE_REF: Pose Index = {poseIndex}, Influence = {influence}  ");
                            break;
                    }
                    if (!context.IsEndOfStream())
                    {
                        (id, length) = context.ReadChunkHeader();
                    }
                }
                if(!context.IsEndOfStream())
                {
                    context.offsetStream(-6);
                }
            }
            return chunks;
        }
    }
    public class SubMesh : MeshChunk
    {
        private string material_name = "";
        public SubMesh(OgreContext context) : base(context) { }
        public override List<MeshChunk> Read()
        {
            Logger.Print("Reading SubMesh");
            material_name = context.ReadString();
            Logger.Print($"Material Name: {material_name}");

            bool useSharedVertices = context.ReadBool();
            Logger.Print($"Use Shared Vertices: {useSharedVertices}");
            var (id, length) = (0, (uint)0);
            uint indexCount = context.ReadUInt32();
            bool idx32bits = context.ReadBool();
            if (indexCount > 0)
            {
                if (idx32bits)
                {
                    uint[] indexes = context.ReadUInts32((int)indexCount);
                    Logger.Print($"Indexes: {string.Join(", ", indexes)}");
                }
                else
                {
                    ushort[] indexes = context.ReadUInts16((int)indexCount);
                    Logger.Print($"Indexes: {string.Join(", ", indexes)}");
                }
            }
            if (!useSharedVertices)
            {
                (id, length) = context.ReadChunkHeader();
                if (id != (int)MeshChunkType.GEOMETRY)
                {
                    throw new Exception($"Expected GEOMETRY chunk, but got 0x{id:X4}");
                }
                new Geometry(context).Read();
            }

            (id, length) = context.ReadChunkHeader();
            while (id == (int)MeshChunkType.M_SUBMESH_BONE_ASSIGNMENT || id == (int)MeshChunkType.M_SUBMESH_OPERATION || id == (int)MeshChunkType.M_SUBMESH_TEXTURE_ALIAS)
            {

                MeshChunk? chunk = null;
                switch (id)
                {
                    case (int)MeshChunkType.M_SUBMESH_OPERATION:
                        chunk = new SubMeshOperation(context);
                        break;
                    case (int)MeshChunkType.M_SUBMESH_BONE_ASSIGNMENT:
                        chunk = new SubMeshBoneAssignment(context);
                        break;
                    case (int)MeshChunkType.M_SUBMESH_TEXTURE_ALIAS:
                        Logger.Print("Reading SubMeshTextureAlias");
                        String aliasName = context.ReadString();
                        String textureName = context.ReadString();
                        Logger.Print($"SubMeshTextureAlias aliasName: {aliasName}, textureName: {textureName}");
                        break;
                }
                if (chunk != null)
                {
                    chunks.Add(chunk);
                    chunks.AddRange(chunk.Read());
                }
                if (!context.IsEndOfStream())
                    (id, length) = context.ReadChunkHeader();
            }
            if (!context.IsEndOfStream())
            {
                context.offsetStream(-6);
            }
            return chunks;
        }   
    }
}
