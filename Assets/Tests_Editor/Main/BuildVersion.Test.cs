// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.Editor.Main
{
    public sealed class BuildVersionTests
    {
        private string m_versionFile;
        private static readonly DateTime BuildDay = new(2026, 10, 27);

        [SetUp]
        public void SetUp()
        {
            m_versionFile = Path.Combine(Path.GetTempPath(), $"foobbuteo-build-version-{Guid.NewGuid():N}.json");
            WriteState();
        }

        [TearDown]
        public void TearDown()
        {
            File.Delete(m_versionFile);
        }

        [Test]
        public void SameDay_BuildsSharePersistedDailyCounter()
        {
            Assert.AreEqual("0.26.1027.3", Reserve(BuildDay), "The next build must follow the tracked daily index.");
            Assert.AreEqual(
                "0.26.1027.4",
                Reserve(BuildDay),
                "A subsequent reservation must read the persisted counter."
            );
            Assert.AreEqual(
                4,
                ReadState()["dailyBuildIndex"].Value<int>(),
                "The counter must remain reserved even without a completed build."
            );
        }

        [TestCase("2026-10-28", "0.26.1028.1")]
        [TestCase("2027-01-02", "0.27.0102.1")]
        public void NewDate_ResetsIndexAndFormatsDate(string date, string expectedVersion)
        {
            var today = DateTime.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

            Assert.AreEqual(
                expectedVersion,
                Reserve(today),
                "A new date must start at index 1 with two-digit year, month and day."
            );
        }

        [Test]
        public void ManualMajor_IsUsedWithoutChangingIt()
        {
            WriteState(major: 3);

            Assert.AreEqual(
                "3.26.1027.3",
                Reserve(BuildDay),
                "The build must use the manually configured major version."
            );
            Assert.AreEqual(
                3,
                ReadState()["major"].Value<int>(),
                "Automatic versioning must preserve the configured major."
            );
        }

        [TestCase(8, 3, 9)]
        [TestCase(3, 8, 9)]
        public void Android_AdvancesBeyondTrackedAndPlayerSettingsCodes(
            int trackedCode,
            int playerCode,
            int expectedCode
        )
        {
            WriteState(androidCode: trackedCode);

            Reserve(BuildDay, isAndroid: true, playerCode: playerCode);
            Reserve(BuildDay, isAndroid: true, playerCode: playerCode);

            Assert.AreEqual(
                expectedCode + 1,
                ReadState()["androidVersionCode"].Value<int>(),
                "Each Android reservation must advance beyond both stored and existing package codes."
            );
        }

        [Test]
        public void OtherPlatforms_PreserveAndroidCounter()
        {
            Reserve(BuildDay, playerCode: 50);

            Assert.AreEqual(
                1,
                ReadState()["androidVersionCode"].Value<int>(),
                "Non-Android builds must not consume Android package codes."
            );
        }

        [Test]
        public void CiVersion_UsesReservationWithoutChangingTrackedCounters()
        {
            var original = File.ReadAllText(m_versionFile);
            var state = InvokeProcessor("ResolveVersion", m_versionFile, BuildDay, "0.26.1103.8", false, 50);

            Assert.AreEqual(
                "0.26.1103.8",
                state.GetType().GetProperty("Version", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state),
                "CI must assign its reserved version without a profile suffix or local date override."
            );
            Assert.AreEqual(
                original,
                File.ReadAllText(m_versionFile),
                "CI must not consume local daily or Android counters."
            );
        }

        [TestCase("0.26.1103.0")]
        [TestCase("0.26.0230.1")]
        [TestCase("0.26.1103.1-r")]
        [TestCase("0.26.1103.2147483648")]
        [TestCase("")]
        public void InvalidCiVersion_StopsBeforeChangingTrackedState(string version)
        {
            var original = File.ReadAllText(m_versionFile);
            Assert.Throws<ArgumentException>(
                () => InvokeProcessor("ResolveVersion", m_versionFile, BuildDay, version, false, 1),
                "Malformed CI versions must stop the build rather than fall back to local numbering."
            );
            Assert.AreEqual(original, File.ReadAllText(m_versionFile), "Rejected CI versions must preserve counters.");
        }

        [TestCase("0.26.1103.1", 24980001)]
        [TestCase("0.26.1103.9999", 24989999)]
        [TestCase("0.26.1104.1", 24990001)]
        public void AndroidCi_UsesStableIncreasingCodesWithoutChangingLocalCounters(string version, int expectedCode)
        {
            var original = File.ReadAllText(m_versionFile);
            var state = InvokeProcessor("ResolveVersion", m_versionFile, BuildDay, version, true, 5);
            var retry = InvokeProcessor("ResolveVersion", m_versionFile, BuildDay.AddDays(1), version, true, 5);

            Assert.AreEqual(
                expectedCode,
                state.GetType().GetField("androidVersionCode").GetValue(state),
                "Android CI codes must increase with the reserved date and daily index."
            );
            Assert.AreEqual(
                expectedCode,
                retry.GetType().GetField("androidVersionCode").GetValue(retry),
                "Retries must reuse their reserved Android code even after UTC midnight."
            );
            Assert.AreEqual(original, File.ReadAllText(m_versionFile), "CI must preserve local counters.");
        }

        [TestCase("0.26.1103.10000")]
        [TestCase("0.19.1231.1")]
        public void AndroidCi_RejectsCodesOutsideTheSupportedRange(string version)
        {
            Assert.Throws<ArgumentException>(
                () => InvokeProcessor("ResolveVersion", m_versionFile, BuildDay, version, true, 5),
                "Android CI must reject dates and daily indices outside its monotonic code range."
            );
        }

        [Test]
        public void LocalVersion_StillReservesTheTrackedCounter()
        {
            var state = InvokeProcessor("ResolveVersion", m_versionFile, BuildDay, null, false, 1);
            Assert.AreEqual(
                "0.26.1027.3",
                state.GetType().GetProperty("Version", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state),
                "Local builds must retain counter allocation and produce a plain version."
            );
            Assert.AreEqual(
                3,
                ReadState()["dailyBuildIndex"].Value<int>(),
                "Local builds must persist their reservation."
            );
        }

        [Test]
        public void CiArguments_RequireAnExplicitReservedVersion()
        {
            Assert.IsNull(
                InvokeProcessor("ReadReleaseVersion", (object)new[] { "-batchmode" }),
                "Local builds need no CI version."
            );
            Assert.AreEqual(
                "0.26.1103.1",
                InvokeProcessor(
                    "ReadReleaseVersion",
                    (object)new[] { "-foobbuteoCi", "-foobbuteoVersion", "0.26.1103.1" }
                ),
                "The container command line must supply the reserved version explicitly."
            );
            Assert.Throws<ArgumentException>(
                () => InvokeProcessor("ReadReleaseVersion", (object)new[] { "-foobbuteoCi" }),
                "Missing CI version arguments must not silently consume local counters."
            );
        }

        [TestCase("2026-10-26", "2026-10-27", 2, typeof(ArgumentException))]
        [TestCase("2026-10-27", "invalid-date", 2, typeof(ArgumentException))]
        [TestCase("2026-10-27", "2026-10-27", -1, typeof(ArgumentException))]
        [TestCase("2026-10-27", "2026-10-27", int.MaxValue, typeof(OverflowException))]
        public void InvalidReservation_LeavesTrackedStateUnchanged(
            string today,
            string previousDate,
            int dailyIndex,
            Type expectedException
        )
        {
            WriteState(previousDate: previousDate, dailyIndex: dailyIndex);
            var original = File.ReadAllText(m_versionFile);
            var date = DateTime.ParseExact(today, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

            Assert.Throws(
                expectedException,
                () => Reserve(date),
                "Invalid or exhausted counters and backward dates must stop the build."
            );
            Assert.AreEqual(
                original,
                File.ReadAllText(m_versionFile),
                "Rejected reservations must leave the version configuration intact."
            );
        }

        [Test]
        public void MissingConfiguration_IsNotSilentlyReset()
        {
            File.Delete(m_versionFile);

            Assert.Throws<FileNotFoundException>(
                () => Reserve(BuildDay),
                "A missing tracked configuration must stop version generation."
            );
        }

        private void WriteState(
            int major = 0,
            string previousDate = "2026-10-27",
            int dailyIndex = 2,
            int androidCode = 1
        )
        {
            var state = new JObject
            {
                ["major"] = major,
                ["lastBuildDate"] = previousDate,
                ["dailyBuildIndex"] = dailyIndex,
                ["androidVersionCode"] = androidCode,
            };
            File.WriteAllText(m_versionFile, state.ToString());
        }

        private JObject ReadState() => JObject.Parse(File.ReadAllText(m_versionFile));

        private string Reserve(DateTime date, bool isAndroid = false, int playerCode = 1)
        {
            object state;
            try
            {
                state = ProcessorType()
                    .GetMethod("ReserveVersion", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { m_versionFile, date, isAndroid, playerCode });
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            return (string)
                state.GetType().GetProperty("Version", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
        }

        private static Type ProcessorType() =>
            UnityEngine
                .Assemblies.CurrentAssemblies.GetLoadedAssemblies()
                .Select(assembly => assembly.GetType("BuildVersionProcessor"))
                .First(type => type != null);

        private static object InvokeProcessor(string methodName, params object[] arguments)
        {
            var method = ProcessorType().GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Build version processor must expose the {methodName} version boundary.");
            try
            {
                return method.Invoke(null, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
