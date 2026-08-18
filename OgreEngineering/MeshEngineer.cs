using KenshiCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

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
        private string filename = "";
        private MeshReader meshreader = new MeshReader();
        public static float[] OgreToBlender(float[] v)
        {
            return new float[]{v[0],-v[2],v[1]};
        }
        public static float[] BlenderToOgre(float[] v)
        {
            return new float[] {v[0],v[2],-v[1]};
        }
        public static float DistanceToBoxEdge(float[] v,float minX, float maxX,float minY, float maxY,float minZ, float maxZ)
        {
            float dx = Math.Min(v[0] - minX, maxX - v[0]);
            float dy = Math.Min(v[1] - minY, maxY - v[1]);
            float dz = Math.Min(v[2] - minZ, maxZ - v[2]);

            return Math.Min(dx, Math.Min(dy, dz));
        }
        private void ParseHeader(OgreContext ctx)
        {
            ctx.loadFlipEndian();
            version = ctx.ReadString();
        }
        public static bool Inside(float[] p, float[] min, float[] max)
        {
            return
                p[0] >= min[0] && p[0] <= max[0] &&
                p[1] >= min[1] && p[1] <= max[1] &&
                p[2] >= min[2] && p[2] <= max[2];
        }
        public static void NormalizeBox(float[] p1, float[] p2, out float[] min, out float[] max)
        {
            min = new float[3];
            max = new float[3];

            min[0] = MathF.Min(p1[0], p2[0]);
            min[1] = MathF.Min(p1[1], p2[1]);
            min[2] = MathF.Min(p1[2], p2[2]);

            max[0] = MathF.Max(p1[0], p2[0]);
            max[1] = MathF.Max(p1[1], p2[1]);
            max[2] = MathF.Max(p1[2], p2[2]);
        }
        public static float TriangleArea(float[] a, float[] b, float[] c)
        {
            float abx = b[0] - a[0];
            float aby = b[1] - a[1];
            float abz = b[2] - a[2];

            float acx = c[0] - a[0];
            float acy = c[1] - a[1];
            float acz = c[2] - a[2];

            float cx = aby * acz - abz * acy;
            float cy = abz * acx - abx * acz;
            float cz = abx * acy - aby * acx;

            return 0.5f * MathF.Sqrt(cx * cx + cy * cy + cz * cz);
        }
        public void LoadMeshFile(string path)
        {
            meshreader = new MeshReader();
            using var fs = File.OpenRead(path);
            var ctx = new OgreContext(new BinaryReader(fs, Encoding.UTF8));
            context = ctx;
             filename = Path.GetFileName(path);
            meshreader.filename = filename;
            string extension = Path.GetExtension(filename).ToLowerInvariant();
            this.Name = filename;

            //Logger.Print($"Loading mesh file: {filename}");
            ParseHeader(ctx);

            meshreader.Read(ctx);
        }
        public string getSkeletonLink()
        {
            return meshreader.getSkeletonLink();
        }
        public bool Intersects(float[] p1, float[] p2,bool fast)
        {
            return meshreader.Intersects(p1, p2, fast);
        }

        public bool IsInfluencedByBone(int boneIndex)
        {
            return meshreader.IsInfluencedByBone(boneIndex);
        }

        public float GetBoneInfluence(int boneIndex)
        {
            return meshreader.GetInfluence(boneIndex);
        }
        public string GetBoneInfluenceInfo()
        {
            return meshreader.GetBoneInfluenceInfo();
        }

        public float IntersectionRatio(float[] p1, float[] p2, int samples)
       {
            if(samples == -1){
                return meshreader.IntersectionRatio(p1, p2);
            }
            return meshreader.IntersectionRatio(p1, p2, samples);
        }
        public static float[] Mid(float[] a, float[] b)
        {
            return new float[]
            {
        (a[0]+b[0])/2,
        (a[1]+b[1])/2,
        (a[2]+b[2])/2
            };
        }
        public static float[] Center(float[] a, float[] b, float[] c)
        {
            return new float[]
            {
        (a[0]+b[0]+c[0])/3,
        (a[1]+b[1]+c[1])/3,
        (a[2]+b[2]+c[2])/3
            };
        }
    }
    /*public abstract class MeshReader
    {
        public abstract string getSkeletonLink();
        public abstract List<float[]> GetVertices();
        public abstract List<int> GetIndexes();
        public abstract List<float[]> GetNormals();

        public abstract void BuildMesh();
        public abstract void Read(OgreContext context);
        public MeshReader() { }

    }
    public class LightMeshReader : MeshReader
    {
        private List<string> skeleton_links = new();
        public override List<float[]> GetVertices()
        {
            return new List<float[]>();
        }

        public override void BuildMesh(){}
        public override List<int> GetIndexes()
        {
            return new List<int>();
        }
        public override List<float[]> GetNormals()
        {
            return new List<float[]>();
        }
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
    }*/
    public class MeshReader
    {
        private List<MeshChunk> chunks = new();
        private List<float[]>? vertices = null;
        //private List<float[]>? normals = null;

        private Dictionary<int, float>? boneWeights = null;
        private List<int>? indices = null;
        public string filename = "";
        public string getSkeletonLink()
        {
            var names = chunks.OfType<SkeletonLink>().Select(x => x.getName()).ToList();

            if (names.Count == 0)
                return "E_NOTFOUND";

            return string.Join(", ", names);
        }
        /*public List<int> GetIndexes()
        {
            if (indices != null)
                return indices;
            indices = this.chunks.OfType<SubMesh>().SelectMany(x => x.Indices).ToList();
            return indices;
        }*/
        public bool IsInfluencedByBone(int boneIndex)
        {
            return chunks.OfType<SubMeshBoneAssignment>().Where(x => x.boneindex == boneIndex).ToList().Count > 0;
        }
        private void CalculateBoneWeights()
        {
            //boneWeights = new Dictionary<int, float> ();
            List<SubMeshBoneAssignment> bones = chunks.OfType<SubMeshBoneAssignment>().ToList();

            // Sum all vertex weights per bone
            boneWeights = bones.GroupBy(x => x.boneindex).ToDictionary(g => g.Key,g => g.Sum(x => x.weight));

            // Normalize to percentages
            float totalWeight = boneWeights.Values.Sum();
            foreach(int i in boneWeights.Keys)
            {
                boneWeights[i] = boneWeights[i] / totalWeight;
            }
        }
        public float GetInfluence(int boneIndex)
        {
            if (boneWeights == null)
                CalculateBoneWeights();
            if (boneWeights!.Keys.Contains(boneIndex))
                return boneWeights[boneIndex];
            return 0f;
            /*List<SubMeshBoneAssignment> bones = chunks.OfType<SubMeshBoneAssignment>().ToList();
            float weight = bones.Where(x => x.boneindex == boneIndex).Select(x => x.weight).Sum();
            float totalWeight = bones.Select(x => x.weight).Sum();
            return weight/totalWeight;*/
        }
        public string GetBoneInfluenceInfo()
        {
            List<SubMeshBoneAssignment> bones = chunks.OfType<SubMeshBoneAssignment>().ToList();
            var boneGroups = bones.GroupBy(x => x.boneindex).Select(g => new { BoneIndex = g.Key, TotalWeight = g.Sum(x => x.weight) }).ToList();
            var totalWeight = boneGroups.Sum(x => x.TotalWeight);
            var influenceInfo = boneGroups.Select(x => $"Bone {x.BoneIndex}: {x.TotalWeight / totalWeight:P2}").ToList();
            return string.Join(", ", influenceInfo);
        }
        public void BuildMesh()
        {
            if (vertices != null)
                return;

            vertices = new List<float[]>();
            indices = new List<int>();

            // shared geometry (if any)
            //var shared = this.chunks.OfType<Geometry>().FirstOrDefault(g => /* shared geometry */);

            /*if (shared != null)
            {
                vertices.AddRange(shared.chunks
                    .OfType<GeometryVertexBuffer>()
                    .SelectMany(v => v.Vertices));
            }*/

            foreach (SubMesh sm in this.chunks.OfType<SubMesh>())
            {
                if (!sm.useSharedVertices && sm.Geometry != null)
                {
                    int offset = vertices.Count;

                    var localVerts = sm.Geometry.chunks.OfType<GeometryVertexBuffer>()
                        .SelectMany(v => v.Vertices);

                    vertices.AddRange(localVerts);

                    foreach (int i in sm.Indices)
                        indices.Add(i + offset);
                }
                else
                {
                    indices.AddRange(sm.Indices);
                }
            }

        }

        public void Read(OgreContext context)
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

        private bool TriangleIntersects(float[] a, float[] b, float[] c, float[] min, float[] max, int depth)
        {

            if (depth <= 0)
                return false;

                float[] ab = { (a[0] + b[0]) * 0.5f,  (a[1] + b[1]) * 0.5f,  (a[2] + b[2]) * 0.5f };

                float[] bc = { (b[0] + c[0]) * 0.5f, (b[1] + c[1]) * 0.5f, (b[2] + c[2]) * 0.5f };

                float[] ca = { (c[0] + a[0]) * 0.5f, (c[1] + a[1]) * 0.5f, (c[2] + a[2]) * 0.5f };

                return TriangleIntersects(a, ab, ca, min, max, depth - 1) || TriangleIntersects(ab, b, bc, min, max, depth - 1) ||
                       TriangleIntersects(ca, bc, c, min, max, depth - 1) || TriangleIntersects(ab, bc, ca, min, max, depth - 1);
        }
        public bool Intersects(float[] p1, float[] p2,bool fast)
            {
                if (vertices == null)
                {
                    BuildMesh();
                }
                if(vertices!.Count == 0)
                {
                    CoreUtils.Print($"Vertices from {filename} are empty.");
                    return false;
                }

                MeshEngineer.NormalizeBox(MeshEngineer.BlenderToOgre(p1),MeshEngineer.BlenderToOgre(p2),out float[] min,out float[] max);

                foreach (var v in vertices)
                {
                    if (v[0] >= min[0] && v[0] <= max[0] &&
                        v[1] >= min[1] && v[1] <= max[1] &&
                        v[2] >= min[2] && v[2] <= max[2])
                    {
                        return true;
                    }
                }

                if (fast)
                    return false;

                for (int i = 0; i < indices!.Count; i += 3)
                {
                    var a = vertices[indices[i]];
                    var b = vertices[indices[i + 1]];
                    var c = vertices[indices[i + 2]];
                if (SegmentIntersectsBox(a, b, min, max) ||
                        SegmentIntersectsBox(b, c, min, max) ||
                        SegmentIntersectsBox(c, a, min, max))
                    {
                        return true;
                    }
                }
            return false;

        }

        public float IntersectionRatio(float[] p1, float[] p2,int samples=8)
        {
            if (vertices == null)
                BuildMesh();

            if (vertices == null || vertices.Count == 0)
                return 0f;

            MeshEngineer.NormalizeBox(
                MeshEngineer.BlenderToOgre(p1),
                MeshEngineer.BlenderToOgre(p2),
                out float[] min,
                out float[] max
            );

            int samplesX = samples;
            int samplesY = samples;
            int samplesZ = samples;

            int inside = 0;
            int total = samplesX * samplesY * samplesZ;

            for (int x = 0; x < samplesX; x++)
            {
                for (int y = 0; y < samplesY; y++)
                {
                    for (int z = 0; z < samplesZ; z++)
                    {
                        float[] point =
                        {
                    min[0] + (x + 0.5f) / samplesX * (max[0] - min[0]),
                    min[1] + (y + 0.5f) / samplesY * (max[1] - min[1]),
                    min[2] + (z + 0.5f) / samplesZ * (max[2] - min[2])
                };

                        if (PointInsideMesh(point))
                            inside++;
                    }
                }
            }

            return (float)inside / total;
        }
        public bool PointInsideMesh(float[] point)
        {
            if (vertices == null)
                BuildMesh();

            if (vertices == null || vertices.Count == 0)
                return false;

            if (indices == null || indices.Count == 0)
                return false;


            float[] center = MeshCenter();


            float[] direction = new float[]  { center[0] - point[0], center[1] - point[1], center[2] - point[2]  };


            Normalize(direction);


            int hits = 0;

            for (int i = 0; i < indices.Count; i += 3)
            {
                float[] v0 = vertices[indices[i]];
                float[] v1 = vertices[indices[i + 1]];
                float[] v2 = vertices[indices[i + 2]];

                if (RayIntersectsTriangle(point, direction, v0, v1, v2))
                {
                    hits++;
                }
            }


            return (hits % 2) == 1;
        }
        public static bool SegmentIntersectsBox(float[] a, float[] b, float[] min, float[] max)
        {
            float tmin = 0f;
            float tmax = 1f;

            for (int i = 0; i < 3; i++)
            {
                float d = b[i] - a[i];

                if (Math.Abs(d) < 1e-6f)
                {
                    if (a[i] < min[i] || a[i] > max[i])
                        return false;
                }
                else
                {
                    float inv = 1f / d;

                    float t1 = (min[i] - a[i]) * inv;
                    float t2 = (max[i] - a[i]) * inv;

                    if (t1 > t2)
                    {
                        float tmp = t1;
                        t1 = t2;
                        t2 = tmp;
                    }

                    tmin = Math.Max(tmin, t1);
                    tmax = Math.Min(tmax, t2);

                    if (tmin > tmax)
                        return false;
                }
            }

            return true;
        }
        private bool RayIntersectsTriangle( float[] origin,float[] direction, float[] v0,float[] v1, float[] v2)
        {
            const float EPSILON = 0.000001f;

            float[] edge1 = Subtract(v1, v0);
            float[] edge2 = Subtract(v2, v0);

            float[] h = Cross(direction, edge2);

            float a = Dot(edge1, h);

            if (a > -EPSILON && a < EPSILON)
                return false;

            float f = 1.0f / a;

            float[] s = Subtract(origin, v0);

            float u = f * Dot(s, h);

            if (u < 0.0f || u > 1.0f)
                return false;

            float[] q = Cross(s, edge1);

            float v = f * Dot(direction, q);

            if (v < 0.0f || u + v > 1.0f)
                return false;

            float t = f * Dot(edge2, q);

            return t > EPSILON;
        }
        private float[] Subtract(float[] a, float[] b)
        {
            return new float[]
            {
        a[0] - b[0],
        a[1] - b[1],
        a[2] - b[2]
            };
        }
        private float Dot(float[] a, float[] b)
        {
            return
                a[0] * b[0] +
                a[1] * b[1] +
                a[2] * b[2];
        }
        private float[] Cross(float[] a, float[] b)
        {
            return new float[]
            {
        a[1] * b[2] - a[2] * b[1],
        a[2] * b[0] - a[0] * b[2],
        a[0] * b[1] - a[1] * b[0]
            };
        }
        private float[] MeshCenter()
        {
            float x = 0;
            float y = 0;
            float z = 0;

            foreach (var v in vertices!)
            {
                x += v[0];
                y += v[1];
                z += v[2];
            }

            float count = vertices.Count;

            return new float[]
            {
        x / count,
        y / count,
        z / count
            };
        }
        private void Normalize(float[] v)
        {
            float len = MathF.Sqrt(
                v[0] * v[0] +
                v[1] * v[1] +
                v[2] * v[2]
            );

            if (len == 0)
                return;

            v[0] /= len;
            v[1] /= len;
            v[2] /= len;
        }
    }
}
