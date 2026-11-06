// SPDX-License-Identifier: AGPL-3.0-only
using Imma.Common;
using NUnit.Framework.Interfaces;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(Tests.Editor.Utils.TestLogging))]

namespace Tests.Editor.Utils
{
    public sealed class TestLogging : ITestRunCallback
    {
        private ImmLogger.LogLevel m_previousLevel;

        public void RunStarted(ITest testsToRun)
        {
            m_previousLevel = ImmLogger.Level;
            ImmLogger.Level = ImmLogger.LogLevel.None;
        }

        public void RunFinished(ITestResult testResults) => ImmLogger.Level = m_previousLevel;

        public void TestStarted(ITest test)
        {
            if (!test.IsSuite)
                ImmLogger.Level = ImmLogger.LogLevel.None;
        }

        public void TestFinished(ITestResult result)
        {
            if (!result.Test.IsSuite)
                ImmLogger.Level = ImmLogger.LogLevel.None;
        }
    }
}
