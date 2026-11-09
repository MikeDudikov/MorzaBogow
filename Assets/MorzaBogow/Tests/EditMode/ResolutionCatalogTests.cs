using System;
using System.Linq;
using MorzaBogow.Settings;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    public sealed class ResolutionCatalogTests
    {
        static DisplayModeSize M(int width, int height) => new DisplayModeSize(width, height);

        [Test]
        public void Build_KeepsOnlyModesInRange()
        {
            var modes = ResolutionCatalog.Build(new[]
            {
                M(1280, 720), M(1920, 1080), M(1919, 1080), M(2560, 1440), M(2561, 1440), M(3840, 2160), M(2560, 1441)
            });
            Assert.That(modes, Is.EqualTo(new[] { M(1920, 1080), M(2560, 1440) }));
        }

        [Test]
        public void Build_RemovesDuplicateRefreshEntriesAndSorts()
        {
            var modes = ResolutionCatalog.Build(new[] { M(2560, 1440), M(1920, 1080), M(2560, 1440), M(2304, 1296), M(2048, 1152) });
            Assert.That(modes, Is.EqualTo(new[] { M(1920, 1080), M(2048, 1152), M(2304, 1296), M(2560, 1440) }));
        }

        [Test]
        public void Build_KeepsOtherAspectRatiosReportedInRange()
        {
            var modes = ResolutionCatalog.Build(new[] { M(1920, 1200), M(2560, 1080) });
            Assert.That(modes, Is.EquivalentTo(new[] { M(1920, 1200), M(2560, 1080) }));
        }

        [Test]
        public void Build_NeverInventsAModeTheDeviceDidNotReport()
        {
            Assert.That(ResolutionCatalog.Build(new[] { M(1920, 1080) }).Contains(M(2560, 1440)), Is.False);
        }

        [Test]
        public void PickDefault_ChoosesHighestAvailable()
        {
            Assert.That(ResolutionCatalog.PickDefault(new[] { M(1920, 1080), M(2560, 1440), M(2304, 1296) }), Is.EqualTo(M(2560, 1440)));
        }

        [Test]
        public void PickDefault_ReturnsNullWhenNothingIsInRange()
        {
            Assert.That(ResolutionCatalog.PickDefault(Array.Empty<DisplayModeSize>()), Is.Null);
        }

        [Test]
        public void PickFallback_KeepsSavedModeWhenAvailable()
        {
            Assert.That(ResolutionCatalog.PickFallback(M(2304, 1296), new[] { M(1920, 1080), M(2304, 1296) }), Is.EqualTo(M(2304, 1296)));
        }

        [Test]
        public void PickFallback_ChoosesClosestPixelCountWhenDisplayChanged()
        {
            Assert.That(ResolutionCatalog.PickFallback(M(2560, 1440), new[] { M(1920, 1080), M(2304, 1296) }), Is.EqualTo(M(2304, 1296)));
        }

        [Test]
        public void Session_RevertsOnceAfterFifteenSeconds()
        {
            var session = new ResolutionChangeSession();
            session.Begin(M(1920, 1080), M(2560, 1440));
            Assert.That(session.Tick(14.9f), Is.False);
            Assert.That(session.Tick(0.2f), Is.True);
            Assert.That(session.State, Is.EqualTo(ResolutionChangeState.Reverted));
            Assert.That(session.Tick(100f), Is.False);
        }

        [Test]
        public void Session_ConfirmStopsTheCountdown()
        {
            var session = new ResolutionChangeSession();
            session.Begin(M(1920, 1080), M(2560, 1440));
            session.Confirm();
            Assert.That(session.Tick(100f), Is.False);
            Assert.That(session.State, Is.EqualTo(ResolutionChangeState.Confirmed));
        }

        [Test]
        public void Session_RejectsOverlappingChange()
        {
            var session = new ResolutionChangeSession();
            session.Begin(M(1920, 1080), M(2560, 1440));
            Assert.Throws<InvalidOperationException>(() => session.Begin(M(2560, 1440), M(1920, 1080)));
        }

        [Test]
        public void Session_RejectsConfirmWithoutPendingChange()
        {
            Assert.Throws<InvalidOperationException>(() => new ResolutionChangeSession().Confirm());
        }
    }
}
