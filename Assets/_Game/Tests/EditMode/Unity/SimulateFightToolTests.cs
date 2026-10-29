using Game.Data;
using Game.EditorTools;
using NUnit.Framework;

namespace Game.Tests.EditMode.UnityTests
{
    public class SimulateFightToolTests
    {
        [Test]
        public void SimulateFirstFight_ProducesReadableLog()
        {
            var cdb = new ConfigDatabase(ConfigValidatorMenu.LoadGameConfig());
            var log = SimulateFightMenu.Run(cdb.Database, cdb.Database.Fights[0], 1, out var result);
            StringAssert.Contains("Aldric", log);
            StringAssert.Contains("=== ", log);
            Assert.AreNotEqual(Game.Core.BattleOutcome.None, result.Outcome);
            UnityEngine.Debug.Log(log);
        }
    }
}
