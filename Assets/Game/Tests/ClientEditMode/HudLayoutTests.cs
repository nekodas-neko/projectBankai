using NUnit.Framework;
using UnityEngine;

namespace Game.Client.Tests
{
    public class HudLayoutTests
    {
        Hud hud;

        [TearDown]
        public void TearDown()
        {
            if (hud != null)
            {
                Object.DestroyImmediate(hud.Root);
            }

            foreach (var system in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(system.gameObject);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TheActionButtonsDoNotOverlapEachOther(bool moveOnRight)
        {
            hud = Hud.Create(moveOnRight, new HudCallbacks());
            var names = new[] { "attack", "skill", "ultimate", "defend" };
            var buttons = new[] { hud.Attack, hud.Special, hud.Ultimate, hud.Defense };

            for (var i = 0; i < buttons.Length; i++)
            {
                for (var j = i + 1; j < buttons.Length; j++)
                {
                    var a = (RectTransform)buttons[i].transform;
                    var b = (RectTransform)buttons[j].transform;
                    var distance = Vector2.Distance(a.anchoredPosition, b.anchoredPosition);
                    var needed = (a.sizeDelta.x + b.sizeDelta.x) * 0.5f;

                    Assert.GreaterOrEqual(distance, needed, $"{names[i]} and {names[j]} overlap");
                }
            }
        }

        [Test]
        public void TheUltimateSitsBelowTheHeightItHadAndAboveTheSkill()
        {
            hud = Hud.Create(false, new HudCallbacks());

            var ultimate = ((RectTransform)hud.Ultimate.transform).anchoredPosition.y;
            var skill = ((RectTransform)hud.Special.transform).anchoredPosition.y;

            Assert.Less(ultimate, 640f);
            Assert.Greater(ultimate, skill);
        }
    }
}
