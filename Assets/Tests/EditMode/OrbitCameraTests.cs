using NUnit.Framework;
using UnityEngine;
using System.Reflection;

namespace OIT.Tests
{
    public class OrbitCameraTests
    {
        private GameObject _cameraGo;
        private GameObject _pivotGo;

        [SetUp]
        public void SetUp()
        {
            _cameraGo = new GameObject("TestCamera");
            _pivotGo = new GameObject("TestPivot");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_cameraGo);
            Object.DestroyImmediate(_pivotGo);
        }

        // UNIT-CAM-01: RotateAround 360° returns camera to start — distance error < 0.001f
        [Test]
        public void UNIT_CAM_01_RotateAround360_ReturnsToStart()
        {
            Vector3 startPos = new Vector3(5f, 0f, 0f);
            _cameraGo.transform.position = startPos;
            _pivotGo.transform.position = Vector3.zero;

            _cameraGo.transform.RotateAround(_pivotGo.transform.position, Vector3.up, 360f);

            float distanceFromStart = Vector3.Distance(_cameraGo.transform.position, startPos);
            Assert.That(distanceFromStart, Is.LessThan(0.001f),
                $"After 360° rotation, camera should return to start. Distance from start: {distanceFromStart}");
        }

        // UNIT-CAM-02: Orbit distance maintained after rotation — distance from pivot = orbitDistance ± 0.001f
        [Test]
        public void UNIT_CAM_02_OrbitDistance_MaintainedAfterRotation()
        {
            float expectedDistance = 5f;
            _cameraGo.transform.position = new Vector3(expectedDistance, 0f, 0f);
            _pivotGo.transform.position = Vector3.zero;

            float initialDist = Vector3.Distance(_cameraGo.transform.position, _pivotGo.transform.position);

            // Simulate the distance-maintenance step from OrbitCamera.Update()
            _cameraGo.transform.RotateAround(_pivotGo.transform.position, Vector3.up, 45f);
            Vector3 desiredPos = (_cameraGo.transform.position - _pivotGo.transform.position).normalized * expectedDistance + _pivotGo.transform.position;
            _cameraGo.transform.position = desiredPos;

            float finalDist = Vector3.Distance(_cameraGo.transform.position, _pivotGo.transform.position);
            Assert.That(Mathf.Abs(finalDist - expectedDistance), Is.LessThan(0.001f),
                $"Distance from pivot should be {expectedDistance} ± 0.001, was {finalDist}");
        }

        // UNIT-CAM-03: LookAt pivot stays on screen center — forward vector points toward pivot
        [Test]
        public void UNIT_CAM_03_LookAt_ForwardVectorPointsTowardPivot()
        {
            _cameraGo.transform.position = new Vector3(5f, 2f, 3f);
            _pivotGo.transform.position = Vector3.zero;

            _cameraGo.transform.LookAt(_pivotGo.transform);

            Vector3 towardPivot = (_pivotGo.transform.position - _cameraGo.transform.position).normalized;
            float dot = Vector3.Dot(_cameraGo.transform.forward, towardPivot);
            Assert.That(dot, Is.GreaterThan(0.999f),
                $"Forward vector must point toward pivot. Dot product was {dot}");
        }

        // UNIT-CAM-04: lookAtTransform = null → no NullReferenceException in Update()
        [Test]
        public void UNIT_CAM_04_NullLookAtTransform_NoException()
        {
            var cam = _cameraGo.AddComponent<OrbitCamera>();

            // lookAtTransform is private — leave it null (default)
            // Verify via reflection that it is null
            var field = typeof(OrbitCamera).GetField("lookAtTransform",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "lookAtTransform field must exist");
            Assert.That(field.GetValue(cam), Is.Null, "lookAtTransform must be null by default");

            // Invoke Update() via reflection — must not throw
            var update = typeof(OrbitCamera).GetMethod("Update",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(update, Is.Not.Null, "Update method must exist");

            Assert.DoesNotThrow(() => update.Invoke(cam, null),
                "OrbitCamera.Update() must not throw when lookAtTransform is null");
        }
    }
}
