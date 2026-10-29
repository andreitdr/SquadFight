using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.ViewModels;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ViewModelBaseTests
    {
        [Test]
        public void CurrencyBar_RaisesOnCurrencyChange_StopsAfterDispose()
        {
            var s = TestSession.Create();
            var vm = new CurrencyBarViewModel(s);
            var changes = new List<string>();
            vm.Changed += changes.Add;
            CollectionAssert.AreEqual(new[] { TestContent.Gold }, vm.CurrencyIds);
            s.Currency.Add(TestContent.Gold, 5);
            Assert.AreEqual(105, vm.Amount(TestContent.Gold));
            CollectionAssert.AreEqual(new[] { TestContent.Gold }, changes);
            vm.Dispose();
            Assert.AreEqual(0, s.Bus.HandlerCount<CurrencyChanged>(), "subscriptions released (no leaks)");
            s.Currency.Add(TestContent.Gold, 5);
            Assert.AreEqual(1, changes.Count);
        }

        private sealed class Probe : ViewModelBase
        {
            private int _v;
            public int V { get => _v; set => Set(ref _v, value, nameof(V)); }
        }

        [Test]
        public void Set_RaisesOnlyOnRealChange()
        {
            var p = new Probe();
            var n = 0;
            p.Changed += _ => n++;
            p.V = 1;
            p.V = 1;
            p.V = 2;
            Assert.AreEqual(2, n);
        }

        [Test]
        public void ErrorKeys_ExistForEveryErrorCode()
        {
            var loc = new TableLocalizer();
            loc.LoadCsv(Fixtures.ReadUiStrings());
            foreach (ErrorCode code in System.Enum.GetValues(typeof(ErrorCode)))
            {
                if (code == ErrorCode.None) continue;
                Assert.IsTrue(loc.HasIn("en", ErrorKeys.For(code)), ErrorKeys.For(code));
            }
        }

        [Test]
        public void Localizer_FallbackPluralsAndLanguageSwitch()
        {
            var loc = new TableLocalizer();
            loc.LoadCsv(Fixtures.ReadUiStrings());
            Assert.AreEqual("Cancel", loc.Get("cancel"));
            Assert.AreEqual("missing_key", loc.Get("missing_key"));
            Assert.AreEqual("1 shard", loc.Plural("shards_count", 1));
            Assert.AreEqual("5 shards", loc.Plural("shards_count", 5));
            var switched = 0;
            loc.LanguageChanged += () => switched++;
            loc.SetLanguage("ro");
            Assert.AreEqual(1, switched);
            Assert.AreEqual("Anulează", loc.Get("cancel"));
            Assert.AreEqual("1 fragment", loc.Plural("shards_count", 1));
            Assert.AreEqual("5 fragmente", loc.Plural("shards_count", 5));
            Assert.AreEqual("25 de fragmente", loc.Plural("shards_count", 25));
            Assert.AreEqual("x", new TableLocalizer().Format("x"), "unknown key formats to itself");
        }
    }
}
