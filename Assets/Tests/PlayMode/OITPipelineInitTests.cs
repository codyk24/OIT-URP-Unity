using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OIT;

namespace OIT.Tests
{
    public class OITPipelineInitTests
    {
        private GameObject _go;
        private OITBufferManager _mgr;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        void CreateManager(long simulatedMemory = 0)
        {
            _go = new GameObject("OITBufferManagerTest");
            if (simulatedMemory > 0)
            {
                _go.SetActive(false);
                _mgr = _go.AddComponent<OITBufferManager>();
                _mgr.simulatedGraphicsMemoryBytes = simulatedMemory;
                _go.SetActive(true);
            }
            else
            {
                _mgr = _go.AddComponent<OITBufferManager>();
            }
        }

        // INT-INIT-01: All 3 OIT buffers non-null after scene load
        [UnityTest]
        public IEnumerator INT_INIT_01_AllThreeBuffers_NonNull()
        {
            CreateManager();
            yield return null;
            Assert.IsNotNull(_mgr.HeadBuffer);
            Assert.IsNotNull(_mgr.NodeBuffer);
            Assert.IsNotNull(_mgr.AtomicCounter);
        }

        // INT-INIT-02: HeadBuffer.count == screenWidth × screenHeight
        [UnityTest]
        public IEnumerator INT_INIT_02_HeadBuffer_Count_MatchesScreen()
        {
            CreateManager();
            yield return null;
            Assert.AreEqual(Screen.width * Screen.height, _mgr.HeadBuffer.count);
        }

        // INT-INIT-03: NodeBuffer.count == screenWidth × screenHeight × MaxLayers
        [UnityTest]
        public IEnumerator INT_INIT_03_NodeBuffer_Count_MatchesScreenTimesMaxLayers()
        {
            CreateManager();
            yield return null;
            Assert.AreEqual(Screen.width * Screen.height * _mgr.MaxLayers, _mgr.NodeBuffer.count);
        }

        // INT-INIT-04: AtomicCounter[0] == 0 after buffer clear pass
        [UnityTest]
        public IEnumerator INT_INIT_04_AtomicCounter_IsZero_AfterClear()
        {
            CreateManager();
            yield return null;
            var data = new uint[1];
            _mgr.AtomicCounter.GetData(data);
            Assert.AreEqual(0u, data[0]);
        }

        // INT-INIT-05: HeadBuffer all-sentinel (0xFFFFFFFF) after buffer clear pass
        [UnityTest]
        public IEnumerator INT_INIT_05_HeadBuffer_AllSentinel_AfterClear()
        {
            CreateManager();
            yield return null;
            var data = new uint[_mgr.HeadBuffer.count];
            _mgr.HeadBuffer.GetData(data);
            Assert.That(data, Is.All.EqualTo(OITBufferManager.HeadSentinel));
        }

        // INT-INIT-06: Low-VRAM mock (500 MB < 1 GB threshold) → MaxLayers=16
        [UnityTest]
        public IEnumerator INT_INIT_06_LowVRAMMock_UsesMaxLayers16()
        {
            CreateManager(simulatedMemory: 500L * 1024L * 1024L);
            yield return null;
            Assert.AreEqual(16, _mgr.MaxLayers);
            Assert.AreEqual(Screen.width * Screen.height * 16, _mgr.NodeBuffer.count);
        }
    }
}
