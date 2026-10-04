using NUnit.Framework;
using ZombieWar.EditorTools;

namespace ZombieWar.Tests
{
    /// G12.8 (04/10): the screens hold serialized references instead of finding widgets by path at
    /// run time. A renamed or missing node used to blank a label silently; now this fails until the
    /// prefab is wired again (HordeCall/UI/Wire Serialized Refs).
    public class SerializedRefsTests
    {
        [TestCaseSource(nameof(Prefabs))]
        public void Prefab_HasEveryReferenceWired(string path)
        {
            var nulls = SerializedRefsWiring.Unwired(path);
            Assert.IsEmpty(nulls, string.Join("\n", nulls));
        }

        static string[] Prefabs => SerializedRefsWiring.Prefabs;
    }
}
