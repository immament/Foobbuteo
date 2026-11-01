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

        [TestCase("0.26.1001.1-d", "-d")]
        [TestCase("0.26.0925.2-f", "-f")]
        [TestCase("0.26.1026.1-r", "-r")]
        [TestCase("0.26.1027.2", "")]
        public void ProfileSuffix_IsPreserved(string currentVersion, string expectedSuffix)
        {
            var suffix = ProcessorType()
                .GetMethod("GetSuffix", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { currentVersion });

            Assert.AreEqual(
                expectedSuffix,
                suffix,
                "Version generation must preserve the existing profile's Web variant suffix."
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
    }
}
