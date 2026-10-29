using System.Collections;
using Game.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class BootFlowTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (var go in Object.FindObjectsByType<GameBootstrap>()) Object.Destroy(go.gameObject);
            AppContext.Reset();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Boot_LoadsMainScene()
        {
            SceneManager.LoadScene(SceneNames.Boot, LoadSceneMode.Single);
            var timeout = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name != SceneNames.Main && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.AreEqual(SceneNames.Main, SceneManager.GetActiveScene().name);
            Assert.IsTrue(AppContext.IsBooted);
        }
    }
}
