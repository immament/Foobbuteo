// SPDX-License-Identifier: AGPL-3.0-only
using Imma.Foobbuteo.Client.UI.Modals;
using NUnit.Framework;

namespace Tests.Editor.Client
{
    public class ModalHostTests
    {
        [Test]
        public void Open_IsExclusiveAndRepeatedOpenPreservesActivePanel()
        {
            using var host = new ModalHostController(new ModalHostState());
            using var first = new ModalPanelController(host, "first", () => true, _ => { });
            using var second = new ModalPanelController(host, "second", () => true, _ => { });

            Assert.IsTrue(first.TryOpen(), "The first panel must open on an empty host.");
            Assert.IsTrue(first.TryOpen(), "Repeated opening must preserve the existing active panel.");
            Assert.IsFalse(second.TryOpen(), "Another panel must not replace an active panel's work.");
            Assert.IsTrue(first.State.IsVisible, "The admitted panel must remain visible.");
            Assert.IsFalse(host.State.BackgroundEnabled, "An active modal must block background UI.");
        }

        [Test]
        public void BusyDismissal_IsRefusedButForcedClosureNotifiesOnce()
        {
            using var host = new ModalHostController(new ModalHostState());
            int closed = 0;
            using var panel = new ModalPanelController(host, "busy", () => false, _ => closed++);
            panel.TryOpen();

            Assert.IsFalse(panel.TryClose(ModalCloseReason.Cancel), "Busy work must refuse ordinary cancellation.");
            host.ForceCloseActive(ModalCloseReason.MatchStarted);
            host.ForceCloseActive(ModalCloseReason.MatchStarted);

            Assert.AreEqual(1, closed, "A forced lifecycle close must notify the feature exactly once.");
            Assert.IsTrue(host.State.BackgroundEnabled, "Forced closure must release background input.");
        }

        [Test]
        public void ActivePanelDisposal_ReleasesHostAndAllowsAnotherPanel()
        {
            using var host = new ModalHostController(new ModalHostState());
            var first = new ModalPanelController(host, "first", () => false, _ => { });
            using var second = new ModalPanelController(host, "second", () => true, _ => { });
            first.TryOpen();
            first.Dispose();

            Assert.IsTrue(second.TryOpen(), "Disposing an active feature must release its modal registration.");
            Assert.IsFalse(first.TryOpen(), "A disposed panel must never re-enter the host.");
        }
    }
}
