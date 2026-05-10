using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using OIT;

namespace OIT.Tests
{
    public class OpaquePassTests
    {
        // INT-OPAQUE-01: OpaquePass injection event is BeforeRenderingOpaques
        [Test]
        public void INT_OPAQUE_01_OpaquePass_HasCorrect_RenderPassEvent()
        {
            var pass = new OpaquePass();
            Assert.AreEqual(RenderPassEvent.BeforeRenderingOpaques, pass.renderPassEvent);
        }

        // INT-OPAQUE-02: OITRenderFeature instantiates and Create() runs without throwing
        [Test]
        public void INT_OPAQUE_02_OITRenderFeature_CreateInstance_NoThrow()
        {
            OITRenderFeature feature = null;
            Assert.DoesNotThrow(() =>
            {
                feature = ScriptableObject.CreateInstance<OITRenderFeature>();
                feature.Create();
            });
            if (feature != null)
                Object.DestroyImmediate(feature);
        }

        // INT-OPAQUE-03: Scene with an opaque cube renders one frame without GPU errors
        [UnityTest]
        public IEnumerator INT_OPAQUE_03_OpaqueScene_RendersFrame_NoGPUErrors()
        {
            GameObject cameraGo = new GameObject("TestCamera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            GameObject cubeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeGo.transform.position = new Vector3(0f, 0f, 3f);

            LogAssert.NoUnexpectedReceived();

            yield return null;

            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(cubeGo);
        }

        // INT-OPAQUE-04: OITRenderFeature instantiated alongside an opaque scene; 3 frames, no errors
        [UnityTest]
        public IEnumerator INT_OPAQUE_04_OITRenderFeature_Scene_Renders3Frames_NoErrors()
        {
            GameObject cameraGo = new GameObject("TestCamera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            GameObject cubeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeGo.transform.position = new Vector3(0f, 0f, 3f);

            OITRenderFeature feature = ScriptableObject.CreateInstance<OITRenderFeature>();
            feature.Create();

            LogAssert.NoUnexpectedReceived();

            yield return null;
            yield return null;
            yield return null;
            
            LogAssert.NoUnexpectedReceived();

            Object.DestroyImmediate(feature);
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(cubeGo);
        }
    }
}
