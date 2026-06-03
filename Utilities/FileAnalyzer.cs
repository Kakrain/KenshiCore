using KenshiCore.OgreEngineering;
using KenshiCore.ReverseEngineering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.Utilities
{
    public class FileAnalyzer
    {
        private static FileAnalyzer? _instance;
        private readonly Dictionary<(string path, Type type), object> _cache = new();
        public static FileAnalyzer Instance
        {
            get
            {
                if (_instance == null) _instance = new FileAnalyzer();
                return _instance;
            }
        }
        private FileAnalyzer() { }
        public T GetOrCompute<T>(string path, Func<string, T> compute)
        {
            var key = (path, typeof(T));

            if (_cache.TryGetValue(key, out var cached))
                return (T)cached;

            T result = compute(path);
            _cache[key] = result!;

            return result;
        }
        public string getSkeletonLink(string filepath) {

            string skeleton = GetOrCompute(
            filepath,
            path =>
            {
                MeshEngineer me = new MeshEngineer();
                me.LoadMeshFile(path);
                return me.getSkeletonLink();
            });
            return skeleton;
        }
        
    }
}
