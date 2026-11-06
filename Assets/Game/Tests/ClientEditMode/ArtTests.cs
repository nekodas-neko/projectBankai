using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Client.Tests
{
    public class ArtTests
    {
        static readonly string[] AnimatorParameters = { "Speed", "Use", "Slot", "Hit", "Dead" };

        [Test]
        public void EveryClassDefaultsToARiggedCharacter()
        {
            foreach (var id in new[] { "sword_and_shield", "healing_mace", "staff" })
            {
                var skins = SkinCatalog.ForClass(id);
                Assert.Greater(skins.Count, 0, id);
                Assert.IsNotNull(skins[0].prefab, $"{id} default skin should be the imported character");
            }
        }

        [Test]
        public void TheBossHasARiggedSkin()
        {
            var skin = SkinCatalog.Get(StableId.From("boss_greendemon"));

            Assert.IsNotNull(skin);
            Assert.AreEqual("boss_greendemon", skin.name);
            Assert.IsNotNull(skin.prefab);
            Assert.Greater(skin.scale.x, 1.5f);
        }

        [Test]
        public void EveryRiggedSkinHasAnAnimatorWithTheExpectedParametersAndTexturedMaterials()
        {
            var rigged = Resources.LoadAll<SkinAsset>(SkinAsset.ResourcesFolder).Where(skin => skin.prefab != null).ToList();
            Assert.GreaterOrEqual(rigged.Count, 12);

            foreach (var skin in rigged)
            {
                var animator = skin.prefab.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator, skin.name);
                Assert.IsNotNull(animator.runtimeAnimatorController, $"{skin.name} controller");
                Assert.IsNotNull(animator.avatar, $"{skin.name} avatar");
                Assert.Greater(animator.runtimeAnimatorController.animationClips.Length, 3, $"{skin.name} clips");

                var names = animator.parameters.Select(p => p.name).ToList();
                foreach (var parameter in AnimatorParameters)
                {
                    CollectionAssert.Contains(names, parameter, skin.name);
                }

                var textured = skin.prefab.GetComponentsInChildren<Renderer>()
                    .SelectMany(r => r.sharedMaterials)
                    .Count(m => m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null);
                Assert.Greater(textured, 0, $"{skin.name} should have a textured material");
            }
        }

        [Test]
        public void CharacterPrefabsAreAboutHumanHeight()
        {
            foreach (var id in new[] { "tank_warrior", "healer_cleric", "mage_wizard" })
            {
                var skin = SkinCatalog.Get(StableId.From(id));
                var renderers = skin.prefab.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                Assert.That(bounds.size.y, Is.InRange(1.2f, 2.6f), id);
            }
        }
    }
}
