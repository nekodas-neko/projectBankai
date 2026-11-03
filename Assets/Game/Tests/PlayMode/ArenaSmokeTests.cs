using System.Collections;
using System.Net;
using Game.Client;
using Game.Core;
using Game.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.PlayMode.Tests
{
    public class ArenaSmokeTests
    {
        static void Press(ActionButton button, Vector2 from, Vector2 to)
        {
            var data = new PointerEventData(EventSystem.current) { pointerId = 0, position = from };
            ExecuteEvents.Execute<IPointerDownHandler>(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            data.position = to;
            ExecuteEvents.Execute<IDragHandler>(button.gameObject, data, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute<IPointerUpHandler>(button.gameObject, data, ExecuteEvents.pointerUpHandler);
        }

        [UnityTest]
        public IEnumerator Skin_ChosenForTheClassIsWhatThePlayerWears()
        {
            HudSettings.ClassName = "staff";
            HudSettings.SetSkin("staff", "mage_crimson");
            SceneManager.LoadScene("Arena");
            yield return new WaitForSeconds(0.6f);
            var bootstrap = Object.FindFirstObjectByType<ArenaBootstrap>();

            Assert.IsTrue(bootstrap.Views.TryGetBodyMaterial(bootstrap.LocalClient.LocalEntity, out var material));
            HudSettings.SetSkin("staff", "mage_arcane");

            Assert.AreEqual("mage_crimson_body", material.name);
        }

        [UnityTest]
        public IEnumerator UsingASkill_PlaysItsCastEffect()
        {
            HudSettings.ClassName = "staff";
            SceneManager.LoadScene("Arena");
            yield return new WaitForSeconds(0.5f);
            var bootstrap = Object.FindFirstObjectByType<ArenaBootstrap>();
            Assert.AreEqual(0, Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length);

            Press(bootstrap.Hud.Attack, new Vector2(100f, 100f), new Vector2(100f, 100f));
            yield return new WaitForSeconds(0.2f);

            Assert.Greater(Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length, 0);
        }

        [UnityTest]
        public IEnumerator DefendButton_DodgesAndTheResourceShows()
        {
            HudSettings.SessionMode = (int)SessionMode.Solo;
            HudSettings.ClassName = "staff";
            SceneManager.LoadScene("Arena");
            yield return new WaitForSeconds(0.5f);
            var bootstrap = Object.FindFirstObjectByType<ArenaBootstrap>();

            Press(bootstrap.Hud.Defense, new Vector2(100f, 100f), new Vector2(100f, 100f));
            yield return new WaitForSeconds(0.1f);

            Assert.IsTrue(bootstrap.LocalClient.TryGetLocalState(out var state));
            Assert.Greater(state.GetCooldown(Entity.DefenseSlot), 0f);
            Assert.AreNotEqual(0, state.Effects & EffectFlags.Invulnerable);
            Assert.Greater(state.MaxResource, 0f);
            Assert.Greater(state.Resource, 0f);
        }

        [UnityTest]
        public IEnumerator UltimateButton_NeedsChargeThenFiresAndEmptiesIt()
        {
            HudSettings.SessionMode = (int)SessionMode.Solo;
            HudSettings.ClassName = "staff";
            SceneManager.LoadScene("Arena");
            yield return new WaitForSeconds(0.5f);
            var bootstrap = Object.FindFirstObjectByType<ArenaBootstrap>();
            var player = bootstrap.Session.Server.FindPlayerEntity(new PlayerId(1));

            Press(bootstrap.Hud.Ultimate, new Vector2(100f, 100f), new Vector2(100f, 100f));
            yield return new WaitForSeconds(0.2f);

            Assert.IsTrue(bootstrap.LocalClient.TryGetLocalState(out var empty));
            Assert.Greater(empty.ChargeMax, 0f);
            Assert.AreEqual(0f, empty.GetCooldown(Entity.UltimateSlot));

            player.Charge = player.ChargeMax;
            yield return new WaitForSeconds(0.2f);
            Press(bootstrap.Hud.Ultimate, new Vector2(100f, 100f), new Vector2(100f, 100f));
            yield return new WaitForSeconds(0.2f);

            Assert.IsTrue(bootstrap.LocalClient.TryGetLocalState(out var fired));
            Assert.Greater(fired.GetCooldown(Entity.UltimateSlot), 0f);
            Assert.AreEqual(0f, fired.Charge, 1f);
        }

        [UnityTest]
        public IEnumerator BossFrame_ShowsTheBossAndWhoItIsFighting()
        {
            HudSettings.SessionMode = (int)SessionMode.Solo;
            HudSettings.ClassName = "staff";
            SceneManager.LoadScene("Arena");
            yield return new WaitForSeconds(0.5f);
            var bootstrap = Object.FindFirstObjectByType<ArenaBootstrap>();

            Assert.IsTrue(bootstrap.Hud.BossFrame.IsVisible);
            Assert.AreEqual("Boss - idle", bootstrap.Hud.BossFrame.Text);

            var player = bootstrap.Session.Server.FindPlayerEntity(new PlayerId(1));
            player.Position = new Vec2(0f, 12f);
            yield return new WaitForSeconds(0.6f);

            Assert.AreEqual("Boss - targeting YOU", bootstrap.Hud.BossFrame.Text);
        }

        [UnityTest]
        public IEnumerator Dungeon_ShowsTheRoomAdvancesAndOffersARestartAfterADefeat()
        {
            HudSettings.SessionMode = (int)SessionMode.Solo;
            HudSettings.ClassName = "staff";
            SceneManager.LoadScene("Arena");
            yield return new WaitForSeconds(0.6f);
            var bootstrap = Object.FindFirstObjectByType<ArenaBootstrap>();
            Assert.AreEqual("Room 1 / 3", bootstrap.Hud.Banner.RoomText);
            Assert.IsFalse(bootstrap.Hud.Banner.RestartVisible);

            foreach (var entity in bootstrap.Session.Server.Entities)
            {
                if (!entity.Owner.IsValid)
                {
                    entity.Health = 0f;
                }
            }

            yield return new WaitForSeconds(0.5f);
            StringAssert.Contains("Room cleared", bootstrap.Hud.Banner.MessageText);

            yield return new WaitForSeconds(3.5f);
            Assert.AreEqual("Room 2 / 3", bootstrap.Hud.Banner.RoomText);

            foreach (var entity in bootstrap.Session.Server.Entities)
            {
                if (entity.Owner.IsValid)
                {
                    entity.Health = 0f;
                }
            }

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual("Defeat", bootstrap.Hud.Banner.MessageText);
            Assert.IsTrue(bootstrap.Hud.Banner.RestartVisible);
        }

        [UnityTest]
        public IEnumerator Style_PicksTheVariantsTheServerUses()
        {
            HudSettings.SessionMode = (int)SessionMode.Solo;
            HudSettings.ClassName = "staff";
            HudSettings.SetStyle("staff", 1);
            try
            {
                SceneManager.LoadScene("Arena");
                yield return new WaitForSeconds(0.5f);
                var bootstrap = Object.FindFirstObjectByType<ArenaBootstrap>();

                var player = bootstrap.Session.Server.FindPlayerEntity(new PlayerId(1));
                Assert.AreEqual(StableId.From("crimson"), player.Variants[0]);
                Assert.AreEqual(StableId.From("frost"), player.Variants[1]);
            }
            finally
            {
                HudSettings.SetStyle("staff", 0);
            }
        }

        [UnityTest]
        public IEnumerator HostMode_AcceptsAJoiningPlayerOverTheNetwork()
        {
            HudSettings.SessionMode = (int)SessionMode.Host;
            HudSettings.ClassName = "staff";
            UdpClientTransport joiner = null;
            try
            {
                SceneManager.LoadScene("Arena");
                yield return new WaitForSeconds(0.5f);
                var bootstrap = Object.FindFirstObjectByType<ArenaBootstrap>();
                Assert.IsNotNull(bootstrap.Host);

                DefinitionLoader.Load(out var classes, out _);
                joiner = new UdpClientTransport(new IPEndPoint(IPAddress.Loopback, bootstrap.Host.Port), "sword_and_shield", 0, "Tester");
                GameClient client = null;
                var waited = 0f;
                while (waited < 4f && (bootstrap.Views.Count < 2 + FirstRoomEnemies() || client == null || !client.HasLocalEntity))
                {
                    joiner.Update(Time.deltaTime);
                    if (client == null && joiner.State == ConnectionState.Connected)
                    {
                        client = new GameClient(joiner.PlayerId, joiner, classes.Get("sword_and_shield"), new ClientConfig { Simulation = joiner.Simulation });
                    }

                    client?.Update(Time.deltaTime);
                    waited += Time.deltaTime;
                    yield return null;
                }

                Assert.AreEqual(ConnectionState.Connected, joiner.State);
                Assert.AreEqual(2 + FirstRoomEnemies(), bootstrap.Views.Count);
                Assert.IsTrue(client.HasLocalEntity);
            }
            finally
            {
                joiner?.Dispose();
                HudSettings.SessionMode = (int)SessionMode.Solo;
            }
        }

        static int FirstRoomEnemies()
        {
            DefinitionLoader.Load(out _, out _, out var dungeons);
            return dungeons.Get("slime_cave").Rooms[0].Spawns.Length;
        }

        [UnityTest]
        public IEnumerator CameraZoom_ScalesTheCameraDistance()
        {
            HudSettings.CameraZoom = 1.6f;
            SceneManager.LoadScene("Arena");
            yield return new WaitForSeconds(1.5f);
            var bootstrap = Object.FindFirstObjectByType<ArenaBootstrap>();

            var height = Camera.main.transform.position.y - bootstrap.Views.LocalRoot.position.y;
            HudSettings.CameraZoom = 1f;

            Assert.AreEqual(26f * 1.6f, height, 0.5f);
        }

        [UnityTest]
        public IEnumerator ActionButtons_TapAndDragFireThroughTheServer()
        {
            SceneManager.LoadScene("Arena");
            yield return new WaitForSeconds(0.5f);
            var bootstrap = Object.FindFirstObjectByType<ArenaBootstrap>();

            Press(bootstrap.Hud.Attack, new Vector2(100f, 100f), new Vector2(100f, 100f));
            Press(bootstrap.Hud.Special, new Vector2(100f, 100f), new Vector2(400f, 100f));
            yield return new WaitForSeconds(0.3f);

            Assert.IsTrue(bootstrap.LocalClient.TryGetLocalState(out var state));
            Assert.Greater(state.GetCooldown(0), 0f);
            Assert.Greater(state.GetCooldown(1), 0f);
            Assert.IsFalse(bootstrap.Hud.Special.IsAiming);
        }

        [UnityTest]
        public IEnumerator Arena_SpawnsEntitiesAndMovesPlayerWithStick()
        {
            SceneManager.LoadScene("Arena");
            yield return null;
            yield return null;

            var bootstrap = Object.FindFirstObjectByType<ArenaBootstrap>();
            Assert.IsNotNull(bootstrap);

            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(1 + FirstRoomEnemies(), bootstrap.Views.Count);
            Assert.IsNotNull(bootstrap.Views.LocalRoot);
            var before = bootstrap.Views.LocalRoot.position;

            bootstrap.Hud.MoveStick.Value = new Vector2(1f, 0f);
            var elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
                bootstrap.Hud.MoveStick.Value = new Vector2(1f, 0f);
            }

            var after = bootstrap.Views.LocalRoot.position;
            Assert.Greater(after.x - before.x, 0.5f);
            Assert.AreEqual(before.z, after.z, 0.05f);
        }
    }
}
