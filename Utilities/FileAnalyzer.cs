using KenshiCore.OgreEngineering;
using KenshiCore.ReverseEngineering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.Utilities
{
    public class FileAnalyzer
    {
        private static FileAnalyzer? _instance;
        private readonly Dictionary<string, MeshEngineer> _cache = new();
        //private readonly Dictionary<(string path, Type type), object> _cache = new();
        public static FileAnalyzer Instance
        {
            get
            {
                if (_instance == null) _instance = new FileAnalyzer();
                return _instance;
            }
        }
        private FileAnalyzer() { }

        public MeshEngineer GetOrComputeMeshEngineer(string path)
        {
            if (_cache.TryGetValue(path, out var cached))
                return cached;

            MeshEngineer result = new MeshEngineer();
            result.LoadMeshFile(path);
            _cache[path] = result;

            return result;
        }
        public string getSkeletonLink(string filepath) {

            return GetOrComputeMeshEngineer(filepath).getSkeletonLink();
        }
        public bool Intersects(string filepath, Array p1, Array p2,bool fast)
        {
            float[] fp1 = p1.Cast<object>().Select(o => Convert.ToSingle(o)).ToArray();
            float[] fp2 = p2.Cast<object>().Select(o => Convert.ToSingle(o)).ToArray();
            return GetOrComputeMeshEngineer(filepath).Intersects(fp1, fp2, fast);
        }
        
        public double GetIntersectionRatio(string filepath, Array p1, Array p2, int samples)
        {
            float[] fp1 = p1.Cast<object>().Select(o => Convert.ToSingle(o)).ToArray();
            float[] fp2 = p2.Cast<object>().Select(o => Convert.ToSingle(o)).ToArray();
            return GetOrComputeMeshEngineer(filepath).IntersectionRatio(fp1, fp2, samples);
        }
        public bool IsInfluencedByBone(string filepath, int boneIndex)
        {
            return GetOrComputeMeshEngineer(filepath).IsInfluencedByBone(boneIndex);
        }
        public float GetBoneInfluence(string filepath, int boneIndex)
        {
            return GetOrComputeMeshEngineer(filepath).GetBoneInfluence(boneIndex);
        }
        public string GetBoneInfluenceInfo(string filepath)
        {
            return GetOrComputeMeshEngineer(filepath).GetBoneInfluenceInfo();
        }
    }
}
