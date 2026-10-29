using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ArchitectureTests
    {
        [TestCase(typeof(SeededRandom))]
        [TestCase(typeof(Game.Save.AssemblyMarker))]
        [TestCase(typeof(Game.Services.AssemblyMarker))]
        public void PureAssembly_HasNoUnityReferences(System.Type marker)
        {
            var asm = marker.Assembly;
            var assemblyName = asm.GetName().Name;
            var unityRefs = asm.GetReferencedAssemblies()
                .Select(r => r.Name)
                .Where(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor") || n.StartsWith("Unity."))
                .ToArray();
            CollectionAssert.IsEmpty(unityRefs, $"{assemblyName} must stay engine-free.");
        }

        [Test]
        public void SeededRandom_SameSeed_SameSequence()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            for (var i = 0; i < 1000; i++) Assert.AreEqual(a.NextInt(0, 1000), b.NextInt(0, 1000));
        }

        [Test]
        public void SeededRandom_StateRoundTrip_Reproduces()
        {
            var a = new SeededRandom(7);
            a.NextDouble();
            var state = a.GetState();
            var expected = a.NextInt(0, 100000);
            var b = new SeededRandom(1);
            b.SetState(state);
            Assert.AreEqual(expected, b.NextInt(0, 100000));
        }

        [Test]
        public void SeededRandom_RangeBounds_Respected()
        {
            var r = new SeededRandom(3);
            for (var i = 0; i < 10000; i++)
            {
                var v = r.NextInt(-5, 5);
                Assert.That(v, Is.InRange(-5, 4));
                var d = r.NextDouble();
                Assert.That(d, Is.GreaterThanOrEqualTo(0).And.LessThan(1));
            }
            Assert.AreEqual(9, r.NextInt(9, 9));
        }

        [Test]
        public void ServiceLocator_RegisterAndResolve()
        {
            var locator = new ServiceLocator();
            var clock = new ManualClock();
            locator.Register<IClock>(clock);
            Assert.AreSame(clock, locator.Get<IClock>());
            Assert.IsFalse(locator.TryGet<IRandom>(out _));
            Assert.Throws<System.InvalidOperationException>(() => locator.Get<IRandom>());
        }
    }
}
