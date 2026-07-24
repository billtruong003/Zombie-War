using BillGameCore;
using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests.EditMode
{
    public sealed class AudioLibraryVariationTests
    {
        [Test]
        public void Get_WithTwoVariants_DoesNotRepeatImmediately()
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            var first = AudioClip.Create("first", 32, 1, 44100, false);
            var second = AudioClip.Create("second", 32, 1, 44100, false);
            library.ReplaceEntries(new[]
            {
                new AudioLibrary.Entry { key = "test", clip = first },
                new AudioLibrary.Entry { key = "test", clip = second },
            });

            var previous = library.Get("test");
            for (var index = 0; index < 20; index++)
            {
                var current = library.Get("test");
                Assert.That(current, Is.Not.SameAs(previous));
                previous = current;
            }

            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(library);
        }

        [Test]
        public void ReplaceEntries_InvalidatesPreviousLookup()
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            var clip = AudioClip.Create("clip", 32, 1, 44100, false);
            library.ReplaceEntries(new[] { new AudioLibrary.Entry { key = "old", clip = clip } });
            Assert.That(library.Get("old"), Is.Not.Null);

            library.ReplaceEntries(new[] { new AudioLibrary.Entry { key = "new", clip = clip } });

            Assert.That(library.Get("old"), Is.Null);
            Assert.That(library.Get("new"), Is.Not.Null);
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(library);
        }
    }
}
