using System.Linq;
using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class CampaignSimulatorTests
    {
        [Test]
        public void HeadlessPlaythrough_CompletesFixtureCampaign()
        {
            var r = CampaignSimulator.Run(TestContent.Create(), new GreedyPolicy(), 1);
            Assert.IsTrue(r.Completed, r.ToMarkdown());
            Assert.AreEqual(4, r.Fights.Count);
            Assert.IsTrue(r.Fights.TrueForAll(f => f.Wins > 0));
            StringAssert.Contains("Completed the campaign", r.ToMarkdown());
        }

        [Test]
        public void SameSeed_IdenticalReport()
        {
            var a = CampaignSimulator.Run(TestContent.Create(), new CasualPolicy(), 7).ToMarkdown();
            var b = CampaignSimulator.Run(TestContent.Create(), new CasualPolicy(), 7).ToMarkdown();
            Assert.AreEqual(a, b);
        }

        [Test]
        public void Policies_ReplayDifferently()
        {
            var casual = CampaignSimulator.Run(TestContent.Create(), new CasualPolicy(), 3);
            var hardcore = CampaignSimulator.Run(TestContent.Create(), new HardcorePolicy(), 3);
            // Hardcore replays every new fight more often (casual may play more in total by losing more).
            Assert.Greater(hardcore.Fights.Sum(f => f.Wins), casual.Fights.Sum(f => f.Wins));
        }
    }
}
