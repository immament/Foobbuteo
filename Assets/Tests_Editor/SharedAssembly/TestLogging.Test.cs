// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections;
using Imma.Common;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Editor.SharedAssembly
{
    public class TestLoggingTests
    {
        [Test]
        public void Logging_IsDisabledByDefault()
        {
            Assert.AreEqual(
                ImmLogger.LogLevel.None,
                ImmLogger.Level,
                "Each test must start with ImmLogger disabled, regardless of earlier tests."
            );
            bool evaluated = false;
            ImmLogger.LError<TestLoggingTests>(() =>
            {
                evaluated = true;
                return "Disabled test logging must not evaluate this message.";
            });
            Assert.IsFalse(evaluated, "Disabled logging must skip error message factories as well as output.");
        }

        [UnityTest]
        public IEnumerator CoroutineLogging_IsDisabledAcrossFrames()
        {
            yield return null;
            Assert.AreEqual(
                ImmLogger.LogLevel.None,
                ImmLogger.Level,
                "Coroutine tests must keep ImmLogger disabled across frames until explicitly enabled."
            );
        }

        [Test]
        public void Logging_CanBeEnabledExplicitlyForOneTest()
        {
            try
            {
                ImmLogger.Level = ImmLogger.LogLevel.Debug;
                ImmLogger.SetLoggerLevel<TestLoggingTests>(ImmLogger.LogLevel.Debug);
                Assert.IsTrue(
                    ImmLogger.IsEnabled<TestLoggingTests>(ImmLogger.LogLevel.Debug),
                    "A test that needs diagnostics must be able to enable its global and type logging levels."
                );
            }
            finally
            {
                ImmLogger.RemoveType<TestLoggingTests>();
            }
        }
    }
}
