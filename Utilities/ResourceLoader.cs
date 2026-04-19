using System.Reflection;
namespace KenshiCore.Utilities
{
    public static class ResourceLoader
    {
        public static Image LoadImage(string resourceName)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (Stream stream = asm.GetManifestResourceStream(resourceName)!)
            {
                return Image.FromStream(stream);
            }
        }
    }
}
