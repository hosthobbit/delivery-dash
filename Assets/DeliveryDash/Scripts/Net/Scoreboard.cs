using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DeliveryDash.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace DeliveryDash
{
    /// <summary>
    /// The public scoreboard (delivery-scores.php on hosthobbit.com). A submission carries the
    /// round's event list, not just a number: the server replays the scoring rules on it and
    /// rejects anything that doesn't add up.
    /// </summary>
    public static class Scoreboard
    {
        public const string Url = "https://hosthobbit.com/games-api/delivery-scores.php";
        public const int Version = 1;

        [Serializable] public class Entry { public string name; public int score; public int delivered; public int houses; public float seconds; public string date; }
        [Serializable] class TopList { public Entry[] scores; }
        [Serializable] class Reply { public bool ok; public int rank; public string error; }

        /// <summary>Events as a compact string: D1..D4 deliveries by grade, M missed, W window, P person, C crash.</summary>
        public static string Encode(IList<RoundEvent> events)
        {
            var sb = new StringBuilder(events.Count * 3);
            foreach (var e in events)
            {
                if (sb.Length > 0) sb.Append(',');
                switch (e.Kind)
                {
                    case RoundEventKind.Delivery: sb.Append('D').Append((int)e.Grade); break;
                    case RoundEventKind.MissedHouse: sb.Append('M'); break;
                    case RoundEventKind.WindowBroken: sb.Append('W'); break;
                    case RoundEventKind.PedestrianHit: sb.Append('P'); break;
                    case RoundEventKind.Crash: sb.Append('C'); break;
                }
            }
            return sb.ToString();
        }

        public static IEnumerator Fetch(Action<List<Entry>, string> done)
        {
            using (var req = UnityWebRequest.Get(Url + "?top=20"))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { done(null, "Couldn't reach the scoreboard. Check your connection."); yield break; }
                try { done(new List<Entry>(JsonUtility.FromJson<TopList>(req.downloadHandler.text).scores ?? new Entry[0]), null); }
                catch { done(null, "The scoreboard sent something odd back."); }
            }
        }

        public static IEnumerator Submit(string name, ScoreSheet sheet, float seconds, int houses, Action<bool, string> done)
        {
            var form = new WWWForm();
            form.AddField("name", name);
            form.AddField("events", Encode(sheet.Events));
            form.AddField("seconds", seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            form.AddField("houses", houses);
            form.AddField("score", sheet.Score);
            form.AddField("v", Version);
            using (var req = UnityWebRequest.Post(Url, form))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();
                Reply r = null;
                try { r = JsonUtility.FromJson<Reply>(req.downloadHandler.text); } catch { }
                if (r != null && r.ok) done(true, $"You're number {r.rank} on the scoreboard!");
                else if (r != null && !string.IsNullOrEmpty(r.error)) done(false, r.error);
                else done(false, "Couldn't reach the scoreboard. Check your connection.");
            }
        }
    }
}
