using NUnit.Framework;
using UnityEngine;
using BillInspector;
using BillInspector.Editor;

namespace BillInspector.Tests.Editor
{
    public class ValidationTests
    {
        private class ValidatedMB : MonoBehaviour
        {
            [BillRequired("Name is required")]
            public string characterName;

            [BillRequired("Weapon required")]
            public GameObject weapon;

            public int hp = 50;

            [BillValidate]
            private void ValidateHP(ValidationResultList results)
            {
                if (hp <= 0)
                    results.AddError("HP must be positive", "hp");
                if (hp > 100)
                    results.AddWarning("HP seems too high", "hp");
            }
        }

        private GameObject _go;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("ValidatedObj");
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Validate_RequiredEmpty_ReturnsError()
        {
            var mb = _go.AddComponent<ValidatedMB>();
            mb.characterName = "";
            var results = BillValidator.Validate(mb);
            Assert.IsTrue(results.HasErrors);
            Assert.IsTrue(results.ErrorCount >= 1);
        }

        [Test]
        public void Validate_RequiredFilled_NoError()
        {
            var mb = _go.AddComponent<ValidatedMB>();
            mb.characterName = "Hero";
            mb.weapon = _go;
            mb.hp = 50;
            var results = BillValidator.Validate(mb);

            // Should not have errors for the filled required fields
            bool hasNameError = false;
            foreach (var e in results.Entries)
            {
                if (e.FieldName == "characterName" && e.Severity == ValidationSeverity.Error)
                    hasNameError = true;
            }
            Assert.IsFalse(hasNameError);
        }

        [Test]
        public void Validate_CustomValidator_HPNegative_ReturnsError()
        {
            var mb = _go.AddComponent<ValidatedMB>();
            mb.characterName = "Hero";
            mb.weapon = _go;
            mb.hp = -10;
            var results = BillValidator.Validate(mb);

            bool hasHPError = false;
            foreach (var e in results.Entries)
            {
                if (e.FieldName == "hp" && e.Severity == ValidationSeverity.Error)
                    hasHPError = true;
            }
            Assert.IsTrue(hasHPError);
        }

        [Test]
        public void Validate_CustomValidator_HPTooHigh_ReturnsWarning()
        {
            var mb = _go.AddComponent<ValidatedMB>();
            mb.characterName = "Hero";
            mb.weapon = _go;
            mb.hp = 150;
            var results = BillValidator.Validate(mb);

            bool hasHPWarning = false;
            foreach (var e in results.Entries)
            {
                if (e.FieldName == "hp" && e.Severity == ValidationSeverity.Warning)
                    hasHPWarning = true;
            }
            Assert.IsTrue(hasHPWarning);
        }

        // ── ValidationResultList ──

        [Test]
        public void ValidationResultList_AddError_IncrementsCount()
        {
            var list = new ValidationResultList();
            list.AddError("test error");
            Assert.AreEqual(1, list.ErrorCount);
            Assert.AreEqual(1, list.Entries.Count);
            Assert.IsTrue(list.HasErrors);
        }

        [Test]
        public void ValidationResultList_AddWarning_IncrementsCount()
        {
            var list = new ValidationResultList();
            list.AddWarning("test warning");
            Assert.AreEqual(1, list.WarningCount);
            Assert.AreEqual(0, list.ErrorCount);
            Assert.IsFalse(list.HasErrors);
        }

        [Test]
        public void ValidationResultList_AddEntry_CorrectCounting()
        {
            var list = new ValidationResultList();
            list.AddEntry(new ValidationEntry { Message = "err", Severity = ValidationSeverity.Error });
            list.AddEntry(new ValidationEntry { Message = "warn", Severity = ValidationSeverity.Warning });
            list.AddEntry(new ValidationEntry { Message = "info", Severity = ValidationSeverity.Info });

            Assert.AreEqual(3, list.Entries.Count);
            Assert.AreEqual(1, list.ErrorCount);
            Assert.AreEqual(1, list.WarningCount);
        }

        [Test]
        public void ValidationResultList_Clear_ResetsAll()
        {
            var list = new ValidationResultList();
            list.AddError("err");
            list.AddWarning("warn");
            list.Clear();

            Assert.AreEqual(0, list.Entries.Count);
            Assert.AreEqual(0, list.ErrorCount);
            Assert.AreEqual(0, list.WarningCount);
            Assert.IsTrue(list.IsValid);
        }
    }
}
