using MorzaBogow.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    public sealed class ObliqueCameraTests
    {
        Camera camera;

        [SetUp]
        public void SetUp() => camera = new GameObject("Camera under test").AddComponent<Camera>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(camera.gameObject);

        [Test]
        public void View_IsOrthographicWithParallelRays()
        {
            ObliqueCamera.Apply(camera, new Vector3(50f, 0f, -20f), ObliqueCamera.DefaultYaw, ObliqueCamera.DefaultTilt, 130f);
            Assert.That(camera.orthographic, Is.True);
            var corner = camera.ViewportPointToRay(new Vector3(0f, 0f, 0f)).direction;
            var center = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)).direction;
            Assert.That(Vector3.Angle(corner, center), Is.LessThan(0.01f));
        }

        [Test]
        public void View_KeepsThePivotInTheCenterAtEveryAngleAndZoom()
        {
            var pivot = new Vector3(50f, 0f, -20f);
            foreach (var (yaw, tilt, zoom) in new[] { (30f, 45f, 130f), (-120f, 25f, 300f), (200f, 80f, 20f) })
            {
                ObliqueCamera.Apply(camera, pivot, yaw, tilt, zoom);
                var point = camera.WorldToViewportPoint(pivot);
                Assert.That(point.x, Is.EqualTo(0.5f).Within(1e-3f));
                Assert.That(point.y, Is.EqualTo(0.5f).Within(1e-3f));
                Assert.That(point.z, Is.GreaterThan(camera.nearClipPlane));
            }
        }

        [Test]
        public void Zoom_ChangesTheVisibleSizeButNotTheAngle()
        {
            ObliqueCamera.Apply(camera, Vector3.zero, 30f, 45f, 300f);
            var far = camera.orthographicSize;
            var angle = camera.transform.rotation;
            ObliqueCamera.Apply(camera, Vector3.zero, 30f, 45f, 20f);
            Assert.That(camera.orthographicSize, Is.LessThan(far));
            Assert.That(Quaternion.Angle(camera.transform.rotation, angle), Is.LessThan(0.01f));
        }

        [Test]
        public void Sun_KeepsTheSameAngleToTheViewAtEveryRotation()
        {
            var first = ObliqueCamera.SunRotation(30f, ObliqueCamera.DefaultTilt) * Vector3.forward;
            var turned = ObliqueCamera.SunRotation(210f, ObliqueCamera.DefaultTilt) * Vector3.forward;
            Assert.That(first.y, Is.EqualTo(turned.y).Within(1e-4f), "Same height above the water.");

            var firstView = Quaternion.Euler(0f, 30f, 0f) * Vector3.forward;
            var turnedView = Quaternion.Euler(0f, 210f, 0f) * Vector3.forward;
            var firstAngle = Vector3.SignedAngle(firstView, new Vector3(first.x, 0f, first.z), Vector3.up);
            var turnedAngle = Vector3.SignedAngle(turnedView, new Vector3(turned.x, 0f, turned.z), Vector3.up);
            Assert.That(firstAngle, Is.EqualTo(ObliqueCamera.SunYawOffset).Within(1e-2f));
            Assert.That(turnedAngle, Is.EqualTo(ObliqueCamera.SunYawOffset).Within(1e-2f));
        }

        [Test]
        public void Sun_SinksAsTheViewSteepens()
        {
            float Height(float tilt) => (ObliqueCamera.SunRotation(30f, tilt) * Vector3.forward).y;
            var degrees = Mathf.Rad2Deg * Mathf.Asin(-Height(ObliqueCamera.MaxTilt));
            Assert.That(degrees, Is.EqualTo(ObliqueCamera.SunHeightAtMaxTilt).Within(1e-2f));
            Assert.That(Height(ObliqueCamera.MinTilt), Is.EqualTo(Height(ObliqueCamera.DefaultTilt)).Within(1e-4f));
        }

        [Test]
        public void Tilt_StaysWithinItsLimits()
        {
            Assert.That(ObliqueCamera.ClampTilt(0f), Is.EqualTo(ObliqueCamera.MinTilt));
            Assert.That(ObliqueCamera.ClampTilt(90f), Is.EqualTo(ObliqueCamera.MaxTilt));
            Assert.That(ObliqueCamera.ClampTilt(60f), Is.EqualTo(60f));
        }
    }
}
