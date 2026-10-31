// Only engine path/log plumbing is stubbed. Save production code is compiled unchanged.
namespace Godot
{
    public static class OS
    {
        public static string TestRoot;
        public static string GetUserDataDir() => TestRoot;
    }
}
namespace STS2MobileIos
{
    public static class PatchHelper
    {
        public static void Log(string message) => System.Console.WriteLine(message);
    }
}
