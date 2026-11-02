using System.Collections;
using Game.Client;
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

            Assert.AreEqual(4, bootstrap.Views.Count);
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
