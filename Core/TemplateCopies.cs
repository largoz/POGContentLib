using UnityEngine;

namespace POGLib.Core
{
    /// <summary>
    /// The library's templates (item shells, shop slots) carry <c>HideFlags.HideAndDontSave</c> so they stay out of
    /// the scenes. A copy made from one keeps those flags: seen 2026-11-19 on a LAN client, where every mod item in a
    /// shop slot reported <c>HideAndDontSave</c> (the host's copies reported <c>None</c>). Unity then leaves the copy
    /// out of <c>FindObjectsOfType</c>, and does not unload a <c>DontSave</c> object with its scene. A spawned copy is
    /// an ordinary scene object, so the flags come off when it spawns, on every peer.
    /// </summary>
    internal static class TemplateCopies
    {
        /// <summary>Clears the hide flags on <paramref name="go"/> and its children; returns how many it changed.</summary>
        public static int Release(GameObject go)
        {
            if (go == null) return 0;
            int changed = 0;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t.gameObject.hideFlags == HideFlags.None) continue;
                t.gameObject.hideFlags = HideFlags.None;
                changed++;
            }
            return changed;
        }
    }
}
