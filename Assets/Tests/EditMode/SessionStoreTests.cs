using System.IO;
using NUnit.Framework;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.Tests
{
    public class SessionStoreTests
    {
        [TearDown]
        public void Cleanup()
        {
            SessionStore.Clear();
        }

        [Test]
        public void Load_NoFile_ReturnsNull()
        {
            SessionStore.Clear();
            Assert.IsNull(SessionStore.Load());
        }

        [Test]
        public void SaveThenLoad_RoundTripsSession()
        {
            var original = new SessionData
            {
                AccessToken = "access-123",
                RefreshToken = "refresh-456",
                ExpiresAtUnixSeconds = 1234567890,
                UserId = "user-789"
            };

            SessionStore.Save(original);
            var loaded = SessionStore.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual("access-123", loaded.AccessToken);
            Assert.AreEqual("refresh-456", loaded.RefreshToken);
            Assert.AreEqual(1234567890, loaded.ExpiresAtUnixSeconds);
            Assert.AreEqual("user-789", loaded.UserId);
        }

        [Test]
        public void Load_CorruptFile_ReturnsNull()
        {
            File.WriteAllText(SessionStore.SessionPath, "not valid json {{{");
            Assert.IsNull(SessionStore.Load());
        }

        [Test]
        public void Clear_RemovesFile()
        {
            SessionStore.Save(new SessionData { AccessToken = "x" });
            SessionStore.Clear();
            Assert.IsFalse(File.Exists(SessionStore.SessionPath));
        }
    }
}
