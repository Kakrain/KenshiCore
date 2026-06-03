using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.Mods
{
    public class UnsupportedModFileException : Exception
    {
        public int FileType { get; }

        public UnsupportedModFileException(int fileType)
            : base($"Unsupported mod file type: {fileType}")
        {
            FileType = fileType;
        }

        public UnsupportedModFileException(int fileType, Exception innerException)
            : base($"Unsupported mod file type: {fileType}", innerException)
        {
            FileType = fileType;
        }
    }
}
