using System.IO;
using NUnit.Framework;

namespace ZombieWar.Tests
{
    /// <summary>
    /// Every renderer feature in the URP renderer assets points at a script (03/10). A feature class
    /// that does not live in a file of its own name loses its script on the next editor start: the
    /// asset keeps the feature with "m_Script: {fileID: 0}", Unity drops it, and nothing warns. The
    /// boss and elite planar shadows disappeared that way after aec6955ba.
    /// </summary>
    public sealed class RendererFeatureAssetTests
    {
        [TestCase("Assets/Settings/Mobile_Renderer.asset")]
        [TestCase("Assets/Settings/PC_Renderer.asset")]
        public void EveryFeatureHasItsScript(string path)
        {
            Assert.That(File.Exists(path), path + " is missing");
            var lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() != "m_Script: {fileID: 0}") continue;
                string name = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
                Assert.Fail($"{path}: a renderer feature has no script ({name})");
            }
        }
    }
}
