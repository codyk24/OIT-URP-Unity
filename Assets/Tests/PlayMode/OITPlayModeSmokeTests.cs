using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace OIT.Tests
{
    public class OITPlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator PlayMode_Assembly_Discovered()
        {
            yield return null;
            Assert.Pass("OIT.Tests.PlayMode assembly is visible to Test Runner.");
        }
    }
}
