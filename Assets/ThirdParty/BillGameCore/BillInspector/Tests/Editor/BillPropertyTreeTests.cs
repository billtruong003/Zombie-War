using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BillInspector;
using BillInspector.Editor;

namespace BillInspector.Tests.Editor
{
    public class BillPropertyTreeTests
    {
        private class TestMB : MonoBehaviour
        {
            public int plainField;

            [BillSlider(0, 100)]
            public float sliderField = 50f;

            [BillReadOnly]
            public string readOnlyField = "immutable";

            [BillBoxGroup("Stats")]
            public int strength = 10;

            [BillBoxGroup("Stats")]
            public int agility = 8;

            [BillButton("Do Something")]
            private void DoSomething() { }
        }

        private GameObject _go;
        private SerializedObject _so;

        [SetUp]
        public void Setup()
        {
            BillPropertyTree.ClearCache();
            _go = new GameObject("TestObj");
            var mb = _go.AddComponent<TestMB>();
            _so = new SerializedObject(mb);
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Build_FindsSerializedProperties()
        {
            var tree = new BillPropertyTree(_so);
            Assert.IsTrue(tree.Properties.Count >= 4, $"Expected >= 4 properties, got {tree.Properties.Count}");
        }

        [Test]
        public void Build_DetectsDrawerAttribute()
        {
            var tree = new BillPropertyTree(_so);
            var slider = tree.Properties.Find(p => p.Name == "sliderField");
            Assert.IsNotNull(slider, "sliderField not found");
            Assert.IsNotNull(slider.DrawerAttribute, "DrawerAttribute should not be null");
            Assert.IsInstanceOf<BillSliderAttribute>(slider.DrawerAttribute);
        }

        [Test]
        public void Build_DetectsMetaAttribute()
        {
            var tree = new BillPropertyTree(_so);
            var ro = tree.Properties.Find(p => p.Name == "readOnlyField");
            Assert.IsNotNull(ro, "readOnlyField not found");
            Assert.IsTrue(ro.HasAttribute<BillReadOnlyAttribute>());
        }

        [Test]
        public void Build_DetectsGroupAttribute()
        {
            var tree = new BillPropertyTree(_so);
            Assert.IsTrue(tree.Groups.ContainsKey("Stats"), "Stats group should exist");
            Assert.AreEqual(2, tree.Groups["Stats"].Count, "Stats should have 2 properties");
        }

        [Test]
        public void Build_DetectsButtonMethods()
        {
            var tree = new BillPropertyTree(_so);
            Assert.AreEqual(1, tree.ButtonMethods.Count, "Should have 1 button method");
            Assert.AreEqual("Do Something", tree.ButtonMethods[0].ButtonAttribute.Label);
        }

        [Test]
        public void Build_PlainFieldHasNoAttributes()
        {
            var tree = new BillPropertyTree(_so);
            var plain = tree.Properties.Find(p => p.Name == "plainField");
            Assert.IsNotNull(plain, "plainField not found");
            Assert.AreEqual(0, plain.AllAttributes.Count);
        }

        [Test]
        public void ClearCache_AllowsRebuild()
        {
            var tree1 = new BillPropertyTree(_so);
            BillPropertyTree.ClearCache();
            var tree2 = new BillPropertyTree(_so);
            Assert.AreEqual(tree1.Properties.Count, tree2.Properties.Count);
        }
    }
}
