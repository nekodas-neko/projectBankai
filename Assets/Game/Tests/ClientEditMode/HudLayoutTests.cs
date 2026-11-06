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
        public void TheResourceIsARingAroundTheSkillButtonThatFillsAndHides()
        {
            hud = Hud.Create(false, new HudCallbacks());

            hud.Resource.Show("rage", 25f, 100f);
            Assert.AreEqual(0.25f, hud.Resource.FillFraction, 0.0001f);
            Assert.AreEqual("rage 25", hud.Resource.Text);
            var ring = hud.Root.transform.Find("SafeArea/ResourceRing");
            Assert.IsTrue(ring.gameObject.activeSelf);
            Assert.AreEqual(((RectTransform)hud.Special.transform).anchoredPosition, ((RectTransform)ring).anchoredPosition, "the ring sits on the skill button");
            Assert.Less(ring.GetSiblingIndex(), hud.Special.transform.GetSiblingIndex(), "and is drawn behind it");

            hud.Resource.Hide();
            Assert.IsFalse(ring.gameObject.activeSelf);
        }

        [Test]
        public void TheAttackButtonShowsItsChargeRingAndHeavyLabel()
        {
            hud = Hud.Create(false, new HudCallbacks());

            Assert.IsTrue(hud.Attack.HoldForHeavy);
            Assert.AreEqual(0f, hud.Attack.ChargeRingFill);
            Assert.AreEqual("Attack", hud.Attack.LabelText);
        }

        [Test]
        public void TheDungeonLogIsNarrowAndAgainstTheRightEdge()
        {
            hud = Hud.Create(false, new HudCallbacks());

            var log = (RectTransform)hud.Root.transform.Find("SafeArea/QuestLog");

            Assert.AreEqual(1f, log.anchorMin.x, "anchored to the right edge");
            Assert.LessOrEqual(log.sizeDelta.x, 330f);
            Assert.GreaterOrEqual(log.anchoredPosition.x, -20f);
        }

        [Test]
        public void TheTopOfTheScreenHasNoOverlaps()
        {
            hud = Hud.Create(false, new HudCallbacks());
            var safe = hud.Root.transform.Find("SafeArea");
            var boss = (RectTransform)safe.Find("BossFrame");
            var log = (RectTransform)safe.Find("QuestLog");
            var objective = (RectTransform)safe.Find("RoomLabel");
            var menu = (RectTransform)safe.Find("Menu");

            Assert.Less(log.anchoredPosition.y, boss.anchoredPosition.y - boss.sizeDelta.y - 44f, "the log starts below the boss frame and its cast bar");

            const float width = 1080f;
            var objectiveRight = width * 0.5f + objective.sizeDelta.x * 0.5f;
            var menuLeft = width + menu.anchoredPosition.x - menu.sizeDelta.x;
            Assert.LessOrEqual(objectiveRight, menuLeft, "the Menu button does not cover the current task");
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
