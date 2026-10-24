using NUnit.Framework;
using UnityEngine;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// The display math behind <see cref="PixelCameraRig.WorldToDisplay01"/> and <see cref="PixelCameraRig.ScreenToGameViewport"/>
    /// (window offset, roll, zoom) round-trips, and <see cref="ShatterProxy"/> palettes pick by weight.
    /// </summary>
    public sealed class DisplayMathTests
    {
        private const int W = 960, H = 540, B = PixelCameraRig.Border;

        [Test]
        public void WindowViewport_RoundTrip([Values(0f, 0.37f, 1f)] float x, [Values(-0.2f, 0.5f, 1.1f)] float y)
        {
            // Arrange
            var offset = new Vector2(0.43f + 5.1f, -0.21f - 4.7f); // remainder + shake, texels
            var window = new Vector2(x, y);

            // Act
            Vector2 viewport = PixelCameraRig.Window01ToViewport(window, W, H, B, offset);
            Vector2 back = PixelCameraRig.ViewportToWindow01(viewport, W, H, B, offset);

            // Assert
            Assert.AreEqual(window.x, back.x, 1e-5f);
            Assert.AreEqual(window.y, back.y, 1e-5f);
        }

        [Test]
        public void WindowCentre_IsViewportCentre_WithoutOffset()
        {
            Vector2 viewport = PixelCameraRig.Window01ToViewport(new Vector2(0.5f, 0.5f), W, H, B, Vector2.zero);
            Assert.AreEqual(0.5f, viewport.x, 1e-6f);
            Assert.AreEqual(0.5f, viewport.y, 1e-6f);
        }

        [Test]
        public void RollZoom_RoundTrip([Values(0f, 0.02f, -0.015f)] float roll, [Values(1f, 1.06f)] float zoom)
        {
            // Arrange
            float aspect = (float)W / H;
            var display = new Vector2(0.81f, 0.17f);

            // Act
            Vector2 source = PixelCameraRig.DisplayToSource(display, roll, zoom, aspect);
            Vector2 back = PixelCameraRig.SourceToDisplay(source, roll, zoom, aspect);

            // Assert
            Assert.AreEqual(display.x, back.x, 1e-5f);
            Assert.AreEqual(display.y, back.y, 1e-5f);
        }

        [Test]
        public void ZoomIn_PullsSourceTowardCentre()
        {
            Vector2 source = PixelCameraRig.DisplayToSource(new Vector2(1f, 1f), 0f, 1.1f, 16f / 9f);
            Assert.Less(source.x, 1f);
            Assert.Less(source.y, 1f);
            Assert.Greater(source.x, 0.5f);
        }

        [Test]
        public void ProxyPalette_PicksByCumulativeWeight()
        {
            // Arrange
            var go = new GameObject("proxy");
            try
            {
                ShatterProxy proxy = go.AddComponent<ShatterProxy>();
                proxy.Configure(Color.gray, false);

                // Act
                proxy.SetPalette(new[] { Color.red, Color.blue }, new[] { 3f, 1f });

                // Assert
                Assert.IsTrue(proxy.HasPalette);
                Assert.AreEqual(Color.red, proxy.PickColor(0.1f));
                Assert.AreEqual(Color.red, proxy.PickColor(0.75f));
                Assert.AreEqual(Color.blue, proxy.PickColor(0.76f));
                Assert.AreEqual(Color.blue, proxy.PickColor(1f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ProxyWithoutPalette_UsesFlatColour()
        {
            var go = new GameObject("proxy");
            try
            {
                ShatterProxy proxy = go.AddComponent<ShatterProxy>();
                proxy.Configure(Color.green, false);
                Assert.IsFalse(proxy.HasPalette);
                Assert.AreEqual(Color.green, proxy.PickColor(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
