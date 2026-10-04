using System.IO;
using System.Linq;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// The source of a class for the tests that check wiring in code: the file itself plus its
    /// partial parts (Weapon.cs + Weapon.Fire.cs + Weapon.Hits.cs), so splitting a class into
    /// parts (G12) does not hide its code from them.
    static class SourceText
    {
        public static string Read(string assetsRelativePath)
        {
            string path = Application.dataPath + assetsRelativePath;
            string dir = Path.GetDirectoryName(path);
            string parts = Path.GetFileNameWithoutExtension(path) + ".*.cs";
            var files = new[] { path }.Concat(Directory.GetFiles(dir, parts).OrderBy(f => f));
            return string.Join("\n", files.Select(File.ReadAllText));
        }
    }
}
