using System.Collections;
using Game.Client;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.PlayMode.Tests
{
    public class ArenaSmokeTests
    {
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
