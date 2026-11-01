// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Common.Utils;
using NUnit.Framework;

namespace Tests.Editor
{
    public class ServerRpcRateLimiterTests
    {
        private const ulong k_clientId = 101;
        private const double k_startTime = 100d;
        private const int k_burstCapacity = 30;

        [SetUp]
        public void SetUp()
        {
            ServerRpcRateLimiter.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ServerRpcRateLimiter.Clear();
        }

        [Test]
        public void TryConsume_BurstCapacityExceeded_RejectsAndLogsOnce()
        {
            ConsumeAllowed(k_clientId, k_startTime, k_burstCapacity);

            Assert.IsFalse(ServerRpcRateLimiter.TryConsume(k_clientId, k_startTime, out bool firstWarning));
            Assert.IsTrue(firstWarning);
            Assert.IsFalse(ServerRpcRateLimiter.TryConsume(k_clientId, k_startTime, out bool repeatedWarning));
            Assert.IsFalse(repeatedWarning);
        }

        [Test]
        public void TryConsume_TimePasses_RefillsAtThirtyRequestsPerSecond()
        {
            ConsumeAllowed(k_clientId, k_startTime, k_burstCapacity);

            double halfSecondLater = k_startTime + 0.5d;
            ConsumeAllowed(k_clientId, halfSecondLater, 15);

            Assert.IsFalse(ServerRpcRateLimiter.TryConsume(k_clientId, halfSecondLater, out _));
        }

        [Test]
        public void TryConsume_WarningIntervalPasses_AllowsAnotherWarning()
        {
            ConsumeAllowed(k_clientId, k_startTime, k_burstCapacity);
            Assert.IsFalse(ServerRpcRateLimiter.TryConsume(k_clientId, k_startTime, out bool firstWarning));
            Assert.IsTrue(firstWarning);

            double halfSecondLater = k_startTime + 0.5d;
            ConsumeAllowed(k_clientId, halfSecondLater, 15);
            Assert.IsFalse(ServerRpcRateLimiter.TryConsume(k_clientId, halfSecondLater, out bool earlyWarning));
            Assert.IsFalse(earlyWarning);

            double oneSecondLater = k_startTime + 1d;
            ConsumeAllowed(k_clientId, oneSecondLater, 15);
            Assert.IsFalse(ServerRpcRateLimiter.TryConsume(k_clientId, oneSecondLater, out bool nextWarning));
            Assert.IsTrue(nextWarning);
        }

        [Test]
        public void TryConsume_DifferentClients_HaveIndependentBudgets()
        {
            ConsumeAllowed(k_clientId, k_startTime, k_burstCapacity);

            Assert.IsFalse(ServerRpcRateLimiter.TryConsume(k_clientId, k_startTime, out _));
            Assert.IsTrue(ServerRpcRateLimiter.TryConsume(k_clientId + 1, k_startTime, out _));
        }

        [Test]
        public void RemoveClient_ExhaustedClient_CreatesFreshBudget()
        {
            ConsumeAllowed(k_clientId, k_startTime, k_burstCapacity);
            Assert.IsFalse(ServerRpcRateLimiter.TryConsume(k_clientId, k_startTime, out _));

            ServerRpcRateLimiter.RemoveClient(k_clientId);

            Assert.IsTrue(ServerRpcRateLimiter.TryConsume(k_clientId, k_startTime, out _));
        }

        [Test]
        public void Clear_ExhaustedClients_CreatesFreshBudgets()
        {
            ConsumeAllowed(k_clientId, k_startTime, k_burstCapacity);
            ConsumeAllowed(k_clientId + 1, k_startTime, k_burstCapacity);

            ServerRpcRateLimiter.Clear();

            Assert.IsTrue(ServerRpcRateLimiter.TryConsume(k_clientId, k_startTime, out _));
            Assert.IsTrue(ServerRpcRateLimiter.TryConsume(k_clientId + 1, k_startTime, out _));
        }

        private static void ConsumeAllowed(ulong clientId, double timestamp, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Assert.IsTrue(
                    ServerRpcRateLimiter.TryConsume(clientId, timestamp, out bool shouldLogWarning),
                    $"Request {i + 1} should be allowed"
                );
                Assert.IsFalse(shouldLogWarning);
            }
        }
    }
}
