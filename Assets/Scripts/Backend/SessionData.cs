using System;

namespace UnderstudyKingdom.Backend
{
    [Serializable]
    public class SessionData
    {
        public string AccessToken;
        public string RefreshToken;
        public long ExpiresAtUnixSeconds;
        public string UserId;

        public bool IsExpired(long nowUnixSeconds, long skewSeconds = 60)
        {
            return nowUnixSeconds >= ExpiresAtUnixSeconds - skewSeconds;
        }
    }
}
