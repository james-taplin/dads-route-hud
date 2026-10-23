using System.Collections.Generic;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>
    /// Resolves a readable name and the game's own short code for a native track, using the
    /// public track identifier parts and the station the game associates with that yard.
    /// Station names are looked up once per yard and reused, so collection stays cheap.
    /// </summary>
    internal static class NativeTrackNames
    {
        private static readonly Dictionary<string, string> stationNames = new Dictionary<string, string>();

        public static void Reset() => stationNames.Clear();

        /// <summary>
        /// Names a track as the player would read it, with the game's code separately so the row
        /// can still be matched against job overviews. Falls back to the object name when the
        /// track carries no yard identity, and never invents a station.
        /// </summary>
        public static void Describe(RailTrack track, out string name, out string code)
        {
            name = "";
            code = "";
            if (track == null)
            {
                name = "Unavailable track";
                return;
            }
            var id = track.LogicTrack()?.ID;
            code = id?.FullID ?? "";
            if (id == null || id.IsGeneric())
            {
                name = RouteNaming.TrackName(null, null, null, null, null, track.name);
                return;
            }
            name = RouteNaming.TrackNameFromParts(id.yardId, id.SignIDSubYardPart, id.SignIDTrackPart,
                StationName(id.yardId), track.name);
        }

        /// <summary>The station's display name for a yard, or empty when the game has none.</summary>
        public static string StationName(string? yardId)
        {
            var yard = (yardId ?? "").Trim();
            if (yard.Length == 0 || yard.Contains("#")) return "";
            if (stationNames.TryGetValue(yard, out var cached)) return cached;
            var name = "";
            var station = StationController.allStations == null ? null : StationController.GetStationByYardID(yard);
            if (station != null && station.stationInfo != null) name = station.stationInfo.Name ?? "";
            // During a scene transition station instances can be temporarily unavailable.
            // Do not permanently cache that transient absence as an unknown station.
            if (!string.IsNullOrWhiteSpace(name)) stationNames[yard] = name;
            return name;
        }

        /// <summary>The station name for the yard a junction's own track belongs to, if any.</summary>
        public static string StationNameForTrack(RailTrack track)
        {
            if (track == null) return "";
            var id = track.LogicTrack()?.ID;
            return id == null ? "" : StationName(id.yardId);
        }
    }
}
