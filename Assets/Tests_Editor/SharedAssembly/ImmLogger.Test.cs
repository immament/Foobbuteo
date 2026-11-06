// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Imma.Common;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace Tests.Editor.SharedAssembly
{
    public class ImmLoggerTests
    {
        private ILogHandler m_previousHandler;
        private ImmLogger.LogLevel m_previousLevel;
        private ImmLogger.LogLevel m_previousDefault;
        private RecordingHandler m_handler;

        [SetUp]
        public void SetUp()
        {
            m_previousHandler = UnityEngine.Debug.unityLogger.logHandler;
            m_previousLevel = ImmLogger.Level;
            m_previousDefault = ImmLogger.DefaultSubLogLevel;
            ImmLogger.Reset();
            ImmLogger.Level = ImmLogger.LogLevel.Trace;
            ImmLogger.DefaultSubLogLevel = ImmLogger.LogLevel.Debug;
            m_handler = new RecordingHandler();
            UnityEngine.Debug.unityLogger.logHandler = m_handler;
            ImmLogger.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            ImmLogger.Reset();
            ImmLogger.Level = m_previousLevel;
            ImmLogger.DefaultSubLogLevel = m_previousDefault;
            UnityEngine.Debug.unityLogger.logHandler = m_previousHandler;
            ImmLogger.Initialize();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DisabledMessages_DoNotEvaluateFactoryOrToString(bool globallyDisabled)
        {
            int calls = 0;
            if (globallyDisabled)
                ImmLogger.Level = ImmLogger.LogLevel.Info;
            else
                ImmLogger.SetLoggerLevel<Caller>(ImmLogger.LogLevel.Info);
            ImmLogger.LTrace<Caller>(() =>
            {
                calls++;
                return "unexpected";
            });
            ImmLogger.LTrace<Caller>(new ThrowingMessage());
            Assert.AreEqual(0, calls, "Suppressed messages must not invoke their message factory.");
            Assert.IsEmpty(m_handler.Messages, "Suppressed messages must not reach the Unity log handler.");
        }

        [Test]
        public void EnabledFactory_RunsOnceAndPreservesCallerInformation()
        {
            int calls = 0;
            string expectedFile = FilePath();
            int expectedLine = LineNumber() + 1;
            ImmLogger.LDebug<Caller>(() =>
            {
                calls++;
                return "accepted message";
            });
            Assert.AreEqual(1, calls, "An accepted message factory must be invoked exactly once.");
            Assert.IsTrue(
                m_handler.Messages.TryDequeue(out string message),
                "The accepted message must reach the handler."
            );
            StringAssert.Contains(
                nameof(EnabledFactory_RunsOnceAndPreservesCallerInformation),
                message,
                "The original caller member must be preserved."
            );
            StringAssert.Contains(expectedFile, message, "The original caller source file must be preserved.");
            StringAssert.Contains(
                $"line=\"{expectedLine}\"",
                message,
                "The link must identify the original caller line."
            );
            StringAssert.Contains("accepted message", message, "The handler must receive the factory result.");
        }

        [Test]
        public void Assertions_EvaluateMessageOnlyForEnabledFailure()
        {
            int calls = 0;
            Func<string> factory = () =>
            {
                calls++;
                return "assertion message";
            };
            ImmLogger.LAssert<Caller>(true, factory);
            ImmLogger.SetLoggerLevel<Caller>(ImmLogger.LogLevel.None);
            ImmLogger.LAssert<Caller>(false, factory);
            ImmLogger.SetLoggerLevel<Caller>(ImmLogger.LogLevel.Assert);
            ImmLogger.LAssert<Caller>(false, factory);
            Assert.AreEqual(1, calls, "Only the enabled failed assertion may evaluate its message.");
            Assert.AreEqual(1, m_handler.Messages.Count, "Only the enabled failed assertion may be emitted.");
        }

        [Test]
        public void InstanceLogging_UsesRuntimeTypeWhileStaticLoggingUsesDeclaredType()
        {
            Caller caller = new DerivedCaller();
            ImmLogger.SetLoggerLevel<Caller>(ImmLogger.LogLevel.None);
            ImmLogger.SetLoggerLevel<DerivedCaller>(ImmLogger.LogLevel.Debug);
            caller.LDebug("runtime type");
            ImmLogger.LDebug<Caller>("static type");
            Assert.AreEqual(
                1,
                m_handler.Messages.Count,
                "A base-typed instance must use its runtime logger while static calls use T."
            );
            Assert.IsTrue(m_handler.Messages.TryPeek(out string message), "The runtime-type message must be recorded.");
            StringAssert.Contains(
                nameof(DerivedCaller),
                message,
                "The runtime class must appear in the instance log prefix."
            );
        }

        [Test]
        public void ExceptionLogging_RespectsTypeFilterAndPreservesException()
        {
            var caller = new Caller();
            Exception exception;
            try
            {
                throw new InvalidOperationException("expected failure");
            }
            catch (InvalidOperationException caught)
            {
                exception = caught;
            }
            ImmLogger.SetLoggerLevel<Caller>(ImmLogger.LogLevel.None);
            caller.LException(exception);
            Assert.IsEmpty(m_handler.Exceptions, "A disabled type must suppress exceptions too.");
            ImmLogger.SetLoggerLevel<Caller>(ImmLogger.LogLevel.Error);
            caller.LException(exception);
            Assert.IsTrue(
                m_handler.Exceptions.TryDequeue(out Exception recorded),
                "An enabled exception must reach the exception handler."
            );
            Assert.AreSame(
                exception,
                recorded,
                "The original exception must be forwarded without losing its stack trace."
            );
            Assert.IsNotEmpty(recorded.StackTrace, "Forwarding must retain the original thrown stack trace.");
        }

        [Test]
        public void Categories_UseStrictestConfiguredLevelAndRefreshExistingLogger()
        {
            var config = ImmLogger.AddType(typeof(Caller), "GameEngine", "Auth");
            Apply("{ 'GameEngine':'Debug', 'Auth':'Info' }");
            Assert.AreEqual(
                ImmLogger.LogLevel.Info,
                config.minLevel,
                "Info and Debug categories must resolve to Info."
            );
            Assert.IsFalse(
                ImmLogger.IsEnabled<Caller>(ImmLogger.LogLevel.Debug),
                "Debug must be suppressed by the Info category."
            );
            Apply("{ 'Auth':'Trace' }");
            Assert.AreEqual(
                ImmLogger.LogLevel.Debug,
                config.minLevel,
                "Updating a category must refresh the existing cached threshold."
            );
            ImmLogger.DefaultSubLogLevel = ImmLogger.LogLevel.Error;
            Assert.AreEqual(
                ImmLogger.LogLevel.Debug,
                config.minLevel,
                "Configured categories replace the default rather than being capped by it."
            );
            ImmLogger.SetLoggerLevel<Caller>(ImmLogger.LogLevel.Trace);
            Apply("{ 'Auth':'None' }");
            Assert.AreEqual(
                ImmLogger.LogLevel.Trace,
                config.minLevel,
                "An explicit type level must override category levels."
            );
            ImmLogger.Level = ImmLogger.LogLevel.Info;
            Assert.IsFalse(
                ImmLogger.IsEnabled<Caller>(ImmLogger.LogLevel.Trace),
                "The global level must still cap explicit type overrides."
            );
        }

        [TestCase(ImmLogger.LogLevel.Info, ImmLogger.LogLevel.Debug, ImmLogger.LogLevel.Info)]
        [TestCase(ImmLogger.LogLevel.Debug, ImmLogger.LogLevel.Trace, ImmLogger.LogLevel.Debug)]
        [TestCase(ImmLogger.LogLevel.None, ImmLogger.LogLevel.Info, ImmLogger.LogLevel.None)]
        [TestCase(ImmLogger.LogLevel.Error, ImmLogger.LogLevel.Warn, ImmLogger.LogLevel.Error)]
        public void CategoryLevels_ResolveVerbosityBoundaries(
            ImmLogger.LogLevel first,
            ImmLogger.LogLevel second,
            ImmLogger.LogLevel expected
        )
        {
            var config = ImmLogger.AddType(typeof(Caller), "First", "Second", "Unconfigured");
            Apply($"{{ 'First':'{first}', 'Second':'{second}' }}");
            Assert.AreEqual(
                expected,
                config.minLevel,
                $"Categories {first} and {second} must resolve to {expected}, ignoring unconfigured tags."
            );
        }

        [Test]
        public void UnityObjectMessages_RespectTypeFilterAndEvaluateFactoryOnce()
        {
            var caller = new GameObject("Unity logger test");
            try
            {
                int calls = 0;
                caller.SetLoggerLevel(ImmLogger.LogLevel.None);
                caller.LogTrace(() =>
                {
                    calls++;
                    return "suppressed";
                });
                caller.LogException(new InvalidOperationException("suppressed exception"));
                Assert.AreEqual(0, calls, "Suppressed Unity logs must not evaluate factories.");
                Assert.IsEmpty(m_handler.Exceptions, "Suppressed Unity exceptions must respect the same type filter.");
                caller.SetLoggerLevel(ImmLogger.LogLevel.Trace);
                caller.LogTrace(() =>
                {
                    calls++;
                    return "accepted Unity message";
                });
                Assert.AreEqual(1, calls, "An enabled Unity factory must run exactly once.");
                Assert.IsTrue(
                    m_handler.Messages.TryPeek(out string message),
                    "The Unity message must reach the handler."
                );
                StringAssert.Contains(
                    "Unity logger test",
                    message,
                    "Unity-object messages must preserve their object-name context."
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(caller);
            }
        }

        [Test]
        public void Registration_IsIdempotentAndCategoriesAreCaseInsensitive()
        {
            var config = ImmLogger.AddType(typeof(Caller), "Auth");
            var same = ImmLogger.AddType(typeof(Caller), "auth", "GameEngine");
            Assert.AreSame(config, same, "Repeated type registration must preserve the existing options object.");
            Assert.AreEqual(2, config.Categories.Count, "Repeated category tags must be deduplicated ignoring case.");
            Apply("{ 'AUTH':'Warn' }");
            Assert.AreEqual(
                ImmLogger.LogLevel.Warn,
                config.minLevel,
                "Category settings must match tags ignoring case."
            );
            CollectionAssert.Contains(
                ImmLogger.GetLoggerByType<Imma.Foobbuteo.Client.ClientAppCtrl>().Categories,
                "Client",
                "Client namespaces must receive their default category."
            );
        }

        [Test]
        public void Configuration_MergesByCategoryWithEditorAboveJavaScriptAboveFile()
        {
            var auth = ImmLogger.AddType(typeof(Caller), "Auth");
            var game = ImmLogger.AddType(typeof(DerivedCaller), "GameEngine");
            Apply("{ 'Auth':'Info', 'GameEngine':'Debug' }");
            Apply("{ 'Auth':'Trace' }", ImmLogger.ConfigurationSource.JavaScript);
            Assert.AreEqual(
                ImmLogger.LogLevel.Trace,
                auth.minLevel,
                "JS must override the same category from the base file."
            );
            Assert.AreEqual(
                ImmLogger.LogLevel.Debug,
                game.minLevel,
                "Omitted JS categories must retain their file values."
            );
            Apply("{ 'Auth':'Warn' }", ImmLogger.ConfigurationSource.Editor);
            Apply("{ 'Auth':'Debug' }", ImmLogger.ConfigurationSource.JavaScript);
            Apply("{ 'Auth':'None' }");
            Assert.AreEqual(
                ImmLogger.LogLevel.Warn,
                auth.minLevel,
                "Editor overrides must remain authoritative regardless of loading order."
            );
        }

        [TestCase("{ 'categories': { 'Auth':'7' } }")]
        [TestCase("{ 'logLevel':'Unknown', 'categories': { 'Auth':'Trace' } }")]
        [TestCase("{ 'logLevel':7 }")]
        [TestCase("{ 'defaultSubLogLevel':'7' }")]
        [TestCase("{ 'defaultSubLogLevel':null }")]
        [TestCase("{ 'categories': { 'Auth':7 } }")]
        [TestCase("{ 'categories': { 'Auth':'Unknown' } }")]
        [TestCase("{ 'categories': { 'Auth':'Debug', ' ':'Info' } }")]
        [TestCase("{ 'categories': [] }")]
        [TestCase("null")]
        public void InvalidConfiguration_DoesNotChangeExistingLevels(string json)
        {
            var config = ImmLogger.AddType(typeof(Caller), "Auth");
            Apply("{ 'Auth':'Info' }");
            Assert.Throws<InvalidOperationException>(
                () => ImmLogger.Configure(LoggingSettings.Parse(JToken.Parse(json))),
                "Malformed logging settings must fail explicitly."
            );
            Assert.AreEqual(
                ImmLogger.LogLevel.Info,
                config.minLevel,
                "Rejected settings must leave previous category levels intact."
            );
        }

        [Test]
        public void ConfiguredLevels_RespectSourcePriorityAndPartialUpdates()
        {
            var caller = ImmLogger.GetLoggerByType<Caller>();
            ImmLogger.Configure(
                LoggingSettings.Parse(JObject.Parse("{ 'logLevel':'Debug', 'defaultSubLogLevel':'Debug' }")),
                ImmLogger.ConfigurationSource.Editor
            );
            ImmLogger.Configure(
                LoggingSettings.Parse(JObject.Parse("{ 'logLevel':'None', 'defaultSubLogLevel':'None' }")),
                ImmLogger.ConfigurationSource.JavaScript
            );
            ImmLogger.Configure(LoggingSettings.Parse(JObject.Parse("{ 'logLevel':'Error' }")));
            ImmLogger.Configure(LoggingSettings.Parse(JObject.Parse("{ 'categories': { 'Auth':'Warn' } }")));
            Assert.IsTrue(
                ImmLogger.IsEnabled<Caller>(ImmLogger.LogLevel.Debug),
                "Editor levels must outrank later browser/file levels and survive category-only updates."
            );
            Assert.AreEqual(
                ImmLogger.LogLevel.Debug,
                caller.minLevel,
                "Changing the configured default must refresh already registered loggers."
            );
        }

        [Test]
        public void Reset_RestoresDefaultsAndDiscardsCategoriesAndOverrides()
        {
            ImmLogger.Configure(
                LoggingSettings.Parse(JObject.Parse("{ 'logLevel':'None', 'defaultSubLogLevel':'None' }")),
                ImmLogger.ConfigurationSource.Editor
            );
            ImmLogger.Level = ImmLogger.LogLevel.None;
            ImmLogger.DefaultSubLogLevel = ImmLogger.LogLevel.Error;
            ImmLogger.AddType(typeof(Caller), "Auth").minLevel = ImmLogger.LogLevel.Warn;
            Apply("{ 'Auth':'None' }");
            ImmLogger.Reset();
            Assert.AreEqual(
                ImmLogger.LogLevel.Info,
                ImmLogger.Level,
                "A new play session must restore the runtime global default."
            );
            Assert.AreEqual(
                ImmLogger.LogLevel.Info,
                ImmLogger.GetLoggerByType<Caller>().minLevel,
                "A new session must discard explicit overrides and category state."
            );
            Assert.IsEmpty(
                ImmLogger.GetLoggerByType<Caller>().Categories,
                "Explicit registration tags must not leak across sessions."
            );
            ImmLogger.Configure(
                LoggingSettings.Parse(JObject.Parse("{ 'logLevel':'Debug', 'defaultSubLogLevel':'Debug' }"))
            );
            Assert.IsTrue(
                ImmLogger.IsEnabled<Caller>(ImmLogger.LogLevel.Debug),
                "Editor level overrides must not survive reset and block a new session's file settings."
            );
        }

        [Test]
        public void EditorMessages_PreserveLevelLabelsAndRichTextWithoutAnsi()
        {
            ImmLogger.LWarn<Caller>("editor message");
            Assert.IsTrue(
                m_handler.Messages.TryDequeue(out string message),
                "An enabled Editor message must reach the handler."
            );
            StringAssert.Contains(
                "<color=orange>W</color> ",
                message,
                "Editor output must preserve its existing warning label."
            );
            StringAssert.Contains("<color=orange>", message, "Editor messages must retain Unity rich-text coloring.");
            StringAssert.DoesNotContain(
                "\u001b",
                message,
                "Editor messages must never contain server ANSI escape codes."
            );
            ImmLogger.GetLoggerByType<Caller>().logLogLevel = false;
            ImmLogger.LWarn<Caller>("without level");
            Assert.IsTrue(
                m_handler.Messages.TryDequeue(out message),
                "Hiding level labels must not suppress the message."
            );
            StringAssert.DoesNotContain(
                "<color=orange>W</color>",
                message,
                "Disabling level labels must still work after separating platform output."
            );
        }

        [Test]
        public void Prefixes_RefreshAfterNameOrColorChangesAndUtcIsInvariant()
        {
            var config = ImmLogger.GetLoggerByType<Caller>();
            config.className = "Changed";
            config.classNameColor = "#123456";
            StringAssert.Contains(
                "<color=#123456>[Changed]",
                config.ClassName(),
                "Cached prefixes must reflect changed names and colors."
            );
            config.logClassName = false;
            Assert.IsEmpty(config.ClassName(), "Disabling class names must hide cached prefixes.");
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
                Assert.AreEqual(
                    "[2026-10-26T14:03:04.123Z] ",
                    LoggerConfig.FormatUtcTime(new DateTime(2026, 10, 26, 14, 3, 4, 123, DateTimeKind.Utc)),
                    "UTC timestamps must use invariant ISO notation and millisecond precision."
                );
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
            config.logUtcTime = true;
            config.logGameTime = false;
            Assert.IsNotEmpty(config.UtcTime(), "UTC output must work independently of game time.");
            Assert.IsEmpty(config.GameTime(), "Disabling game time must not depend on the UTC flag.");
            config.logUtcTime = false;
            config.logGameTime = true;
            Assert.IsEmpty(config.UtcTime(), "UTC output must respect its own flag.");
            Assert.IsNotEmpty(config.GameTime(), "Game-time output must work independently of UTC.");
        }

        [Test]
        public void ConcurrentConfigurationAndLogging_KeepRegistrationSafe()
        {
            var settings = LoggingSettings.Parse(JObject.Parse("{ 'categories': { 'Auth':'Debug' } }"));
            Assert.DoesNotThrow(
                () =>
                    Parallel.For(
                        0,
                        100,
                        index =>
                        {
                            ImmLogger.AddType(typeof(Caller), "Auth");
                            ImmLogger.Configure(settings);
                            ImmLogger.LDebug<Caller>(() => $"worker {index}");
                        }
                    ),
                "Concurrent configuration and plain logging must not corrupt shared state or use Unity main-thread APIs."
            );
            Assert.AreEqual(
                100,
                m_handler.Messages.Count,
                "Each accepted worker-thread log must be emitted exactly once."
            );
        }

        [Test]
        public void DisabledLogging_ReportsWarmedUpAllocationAndTimingComparison()
        {
            const int iterations = 10000;
            const int benchmarkValue = 42;
            int value = benchmarkValue;
            var caller = new Caller();
            ImmLogger.Level = ImmLogger.LogLevel.None;
            Action eager = () => caller.LTrace($"value {value}");
            Action lazy = () =>
            {
                int snapshot = value;
                caller.LTrace(() => $"value {snapshot}");
            };
            Action nonCapturing = () => caller.LTrace(static () => $"value {benchmarkValue}");
            Action guarded = () =>
            {
                if (ImmLogger.IsEnabled<Caller>(ImmLogger.LogLevel.Trace))
                    caller.LTrace($"value {value}");
            };
            Measure("eager", eager, iterations);
            Measure("lazy capturing", lazy, iterations);
            Measure("lazy noncapturing", nonCapturing, iterations);
            Measure("guarded", guarded, iterations);
            Assert.IsEmpty(m_handler.Messages, "All benchmark paths must remain suppressed.");
        }

        private static void Measure(string name, Action action, int iterations)
        {
            for (int index = 0; index < 100; index++)
                action();
            using var allocations = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal,
                "GC.Alloc",
                iterations * 4,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread
            );
            Assert.IsTrue(allocations.Valid, "The allocation comparison requires Unity's GC.Alloc recorder.");
            allocations.Reset();
            allocations.Start();
            long start = Stopwatch.GetTimestamp();
            for (int index = 0; index < iterations; index++)
                action();
            long elapsed = Stopwatch.GetTimestamp() - start;
            allocations.Stop();
            string result =
                $"{name}: {allocations.Count} GC allocation events, {elapsed * 1000.0 / Stopwatch.Frequency:F3} ms for {iterations} calls";
            TestContext.WriteLine(result);
            System.IO.File.AppendAllText("Temp/ImmLogger-benchmark.txt", result + Environment.NewLine);
        }

        private static void Apply(
            string categories,
            ImmLogger.ConfigurationSource source = ImmLogger.ConfigurationSource.BaseFile
        ) => ImmLogger.Configure(LoggingSettings.Parse(JObject.Parse("{ 'categories': " + categories + " }")), source);

        private static string FilePath([CallerFilePath] string path = "") => path;

        private static int LineNumber([CallerLineNumber] int line = 0) => line;

        private class Caller { }

        private sealed class DerivedCaller : Caller { }

        private sealed class ThrowingMessage
        {
            public override string ToString() => throw new InvalidOperationException("Message must be suppressed.");
        }

        private sealed class RecordingHandler : ILogHandler
        {
            public ConcurrentQueue<string> Messages { get; } = new();
            public ConcurrentQueue<Exception> Exceptions { get; } = new();

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args) =>
                Messages.Enqueue(string.Format(format, args));

            public void LogException(Exception exception, UnityEngine.Object context) => Exceptions.Enqueue(exception);
        }
    }
}
