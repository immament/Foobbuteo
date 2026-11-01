using System;
using System.Collections;
using System.Runtime.CompilerServices;
using Imma.Common;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tests.Editor.Utils
{
    internal class TestUtils
    {
        public static void ClearBindingsExcept(VisualElement root, params VisualElement[] boundRoots)
        {
            foreach (var element in root.Query<VisualElement>().ToList())
            {
                if (!Array.Exists(boundRoots, scope => scope == element || scope.Contains(element)))
                    element.ClearBindings();
            }
        }

        [HideInCallstack]
        public static IEnumerator WaitForUi(
            Func<bool> condition,
            string description,
            double timeoutSeconds = 2,
            [CallerMemberName] string callerMember = "",
            [CallerFilePath] string callerFile = "",
            [CallerLineNumber] int callerLine = 0
        )
        {
            double deadline = EditorApplication.timeSinceStartup + timeoutSeconds;

            while (!condition())
            {
                if (EditorApplication.timeSinceStartup >= deadline)
                    Assert.Fail(
                        $"Timed out waiting for '{description}'.\n"
                            + ImmLogger.FormatMsg("", callerMember, callerFile, callerLine)
                    );

                yield return null;
            }
        }

        [HideInCallstack]
        public static IEnumerator WaitForUi(
            Func<bool> condition,
            Func<string> description,
            double timeoutSeconds = 2,
            [CallerMemberName] string callerMember = "",
            [CallerFilePath] string callerFile = "",
            [CallerLineNumber] int callerLine = 0
        )
        {
            double deadline = EditorApplication.timeSinceStartup + timeoutSeconds;

            while (!condition())
            {
                if (EditorApplication.timeSinceStartup >= deadline)
                    Assert.Fail(
                        $"Timed out waiting for '{description()}'.\n"
                            + ImmLogger.FormatMsg("", callerMember, callerFile, callerLine)
                    );

                yield return null;
            }
        }

        public static IEnumerator WaitForBindings(
            Func<bool> ready,
            [CallerMemberName] string callerMember = "",
            [CallerFilePath] string callerFile = "",
            [CallerLineNumber] int callerLine = 0
        )
        {
            double deadline = EditorApplication.timeSinceStartup + 2;
            while (!ready() && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(
                ready(),
                "UI bindings did not settle within five seconds.\n"
                    + ImmLogger.FormatMsg("", callerMember, callerFile, callerLine)
            );
        }

        // private static IEnumerator WaitFor(Func<bool> condition, string message)
        // {
        //     const double timeoutSeconds = 5;
        //     double deadline = EditorApplication.timeSinceStartup + timeoutSeconds;
        //     while (!condition() && EditorApplication.timeSinceStartup < deadline)
        //         yield return null;
        //     Assert.IsTrue(condition(), message);
        // }
    }
}
