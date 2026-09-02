using System;
using System.IO;
using UnityEngine;

namespace UnderstudyKingdom.Backend
{
    /// <summary>
    /// Local JSON persistence for SessionData, mirroring
    /// UnderstudyKingdom.Core.SaveService's exact pattern (Application.persistentDataPath,
    /// JsonUtility, defensive corrupt-file handling -- never throws). See
    /// docs/superpowers/specs/2026-09-02-client-backend-integration-design.md.
    /// </summary>
    public static class SessionStore
    {
        private const string FileName = "backend_session.json";

        public static string SessionPath => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save(SessionData session)
        {
            File.WriteAllText(SessionPath, JsonUtility.ToJson(session));
        }

        public static SessionData Load()
        {
            if (!File.Exists(SessionPath))
            {
                return null;
            }

            try
            {
                string raw = File.ReadAllText(SessionPath);
                string trimmed = raw.TrimStart();

                if (trimmed.Length == 0 || trimmed[0] != '{')
                {
                    return null;
                }

                return JsonUtility.FromJson<SessionData>(raw);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Clear()
        {
            if (File.Exists(SessionPath))
            {
                File.Delete(SessionPath);
            }
        }
    }
}
