using NUnit.Framework;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.Tests
{
    public class SessionDataTests
    {
        [Test]
        public void IsExpired_WellBeforeExpiry_ReturnsFalse()
        {
            var session = new SessionData { ExpiresAtUnixSeconds = 1000 };
            Assert.IsFalse(session.IsExpired(nowUnixSeconds: 500, skewSeconds: 60));
        }

        [Test]
        public void IsExpired_PastExpiry_ReturnsTrue()
        {
            var session = new SessionData { ExpiresAtUnixSeconds = 1000 };
            Assert.IsTrue(session.IsExpired(nowUnixSeconds: 1500, skewSeconds: 60));
        }

        [Test]
        public void IsExpired_ExactlyAtSkewBoundary_ReturnsTrue()
        {
            var session = new SessionData { ExpiresAtUnixSeconds = 1000 };
            Assert.IsTrue(session.IsExpired(nowUnixSeconds: 940, skewSeconds: 60));
        }

        [Test]
        public void IsExpired_JustOutsideSkewWindow_ReturnsFalse()
        {
            var session = new SessionData { ExpiresAtUnixSeconds = 1000 };
            Assert.IsFalse(session.IsExpired(nowUnixSeconds: 939, skewSeconds: 60));
        }
    }
}
