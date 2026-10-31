using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode.UnityTests
{
    /// <summary>iPad / tablet layout: canvas size from the physical screen, grids add columns instead of stretching cells.</summary>
    public class TabletLayoutTests
    {
        [TestCase(1080, 1920, 401)] // 16:9 phone
        [TestCase(1179, 2556, 460)] // iPhone 15 Pro
        [TestCase(1080, 2400, 400)] // 20:9 Android
        [TestCase(640, 480, 0)]     // landscape window (batch-mode tests, Editor): portrait-only game
        public void Phones_KeepPhoneReference(int w, int h, float dpi) =>
            Assert.AreEqual(0f, AdaptiveCanvasScaler.TabletCanvasWidth(w, h, dpi));

        [TestCase(1488, 2266, 326, 1369)] // iPad mini
        [TestCase(1668, 2388, 264, 1895)] // iPad Pro 11"
        [TestCase(1640, 2360, 264, 1864)] // iPad 10th gen
        [TestCase(2064, 2752, 264, 2345)] // iPad Pro 13"
        [TestCase(1200, 1920, 224, 1607)] // 16:10 Android tablet
        public void Tablets_CanvasFollowsPhysicalWidth(int w, int h, float dpi, float expected) =>
            Assert.AreEqual(expected, AdaptiveCanvasScaler.TabletCanvasWidth(w, h, dpi), 1f);

        [Test]
        public void Tablets_CanvasIsNeverSmallerThanThePhoneRule()
        {
            // The old phone rule (match 0.5 on 1080×1920) gave an iPad 11" a ~1246-wide canvas: everything ~1.5× too big.
            Assert.GreaterOrEqual(AdaptiveCanvasScaler.TabletCanvasWidth(1536, 2048, 0), AdaptiveCanvasScaler.MinTabletWidth);
            Assert.AreEqual(AdaptiveCanvasScaler.MaxTabletWidth, AdaptiveCanvasScaler.TabletCanvasWidth(2732, 3640, 264));
        }

        [Test]
        public void UnknownDpi_UsesIPadFallback() =>
            Assert.AreEqual(AdaptiveCanvasScaler.TabletCanvasWidth(1668, 2388, AdaptiveCanvasScaler.FallbackDpi), AdaptiveCanvasScaler.TabletCanvasWidth(1668, 2388, 0));

        [Test]
        public void Grid_OnPhones_IsUnchanged()
        {
            var cell = (1080f - 36 * 2 - 24 * 2) / 3;
            var (columns, width, left) = RecycleList.GridFor(1080, 3, cell, 24, 36);
            Assert.AreEqual(3, columns);
            Assert.AreEqual(cell, width, 0.01f);
            Assert.AreEqual(36, left, 0.01f);
            // Tall phones are narrower than the reference: same columns, slightly narrower cells.
            (columns, width, left) = RecycleList.GridFor(978, 3, cell, 24, 36);
            Assert.AreEqual(3, columns);
            Assert.Less(width, cell);
            Assert.AreEqual(36, left, 0.01f);
        }

        [TestCase(1400, 3)] // iPad mini: cells capped, grid centred
        [TestCase(1895, 5)] // iPad Pro 11"
        [TestCase(2100, 5)] // iPad Pro 13" (wide column cap)
        public void Grid_OnTablets_AddsColumnsAndKeepsCellSize(float listWidth, int expectedColumns)
        {
            var cell = (1080f - 36 * 2 - 24 * 2) / 3;
            var (columns, width, left) = RecycleList.GridFor(listWidth, 3, cell, 24, 36);
            Assert.AreEqual(expectedColumns, columns);
            Assert.GreaterOrEqual(width, cell - 0.01f);
            Assert.LessOrEqual(width, cell * RecycleList.MaxCellGrowth + 0.01f);
            Assert.GreaterOrEqual(left, 36 - 0.01f);
            Assert.AreEqual(listWidth, left * 2 + columns * width + (columns - 1) * 24, 0.5f, "grid is centred");
        }

        [Test]
        public void SingleColumnList_StaysOneColumn()
        {
            var (columns, width, left) = RecycleList.GridFor(2000, 1, 1080 - 48, 20, 24);
            Assert.AreEqual(1, columns);
            Assert.AreEqual(2000 - 48, width, 0.01f);
            Assert.AreEqual(24, left, 0.01f);
        }
    }
}
