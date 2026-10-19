using System;
using System.Collections.Generic;
using System.Linq;
using DV.Logic.Job;
using DV.Simulation.Controllers;
using DV.Utils;
using QuantitiesNet;
using UnityEngine;
using static QuantitiesNet.Units;

namespace DvMod.HeadsUpDisplay
{
    public static class EventList
    {
        private static readonly List<(string distance, string description)> displayedRows = new List<(string, string)>();
        private static float measuredDistanceWidth;
        public static int RowCount => displayedRows.Count;
        public static float PreferredWidth { get; private set; }

        private static string GetJunctionEventDescription(JunctionEvent e, TrainCar currentCar)
        {
            var description = TrackFollower.DescribeJunctionBranches(e.junction);
            var car = GetCarOnJunction(e.junction);
            var carText = (car is Car && car != currentCar.logicCar)
                ? $" <color=orange>({car.ID})</color>" : "";
            return description + carText;
        }

        private static string GetSpeedLimitEventDescription(SpeedLimitEvent e, TrainCar currentCar)
        {
            var currentSpeed = Mathf.Abs(currentCar.GetForwardSpeed() * 3.6f);
            var color = "white";
            if (currentSpeed > e.limit + 5f)
                color = e.span < 500f ? "red" : e.span < 1000f ? "orange" : "yellow";
            else if (currentSpeed < e.limit - 10f)
                color = "lime";
            return $"<color={color}>{e.limit} km/h</color>";
        }

        private static string PlainText(string? value) => (value ?? "")
            .Replace('<', '‹').Replace('>', '›').Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');

        private static string AspectColor(string? value)
        {
            var hex = (value ?? "").Trim();
            if (hex.StartsWith("#", StringComparison.Ordinal)) hex = hex.Substring(1);
            if ((hex.Length == 6 || hex.Length == 8) && hex.All(c =>
                (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return "#" + hex;
            return "#C0C0C0";
        }

        private static string ReservationText(string? value) => string.IsNullOrWhiteSpace(value)
            ? "" : $"  <color=#FFBD59>[{PlainText(value)}]</color>";

        private static string SignalDescription(SignalTrackEvent signal)
        {
            var name = string.IsNullOrWhiteSpace(signal.Name) ? "Signal" : PlainText(signal.Name);
            var aspect = string.IsNullOrWhiteSpace(signal.Aspect) ? "Unknown" : PlainText(signal.Aspect);
            return $"<color={AspectColor(signal.ColorHex)}>{name}</color>  {aspect}{ReservationText(signal.Reservation)}";
        }

        private static string TrackDescription(TrackChangeEvent track)
        {
            var reservation = track.Track == null ? "" : OptionalSignalTelemetry.GetTrackReservation(track.Track);
            var name = track.ID.IsGeneric() && track.Track != null ? track.Track.name : track.ID.ToString();
            return PlainText(name) + ReservationText(reservation);
        }

        private static void CaptureRows()
        {
            displayedRows.Clear();
            var car = PlayerManager.Car ? PlayerManager.Car : PlayerManager.LastLoco;
            if (car == null) return;

            var trackInfoSettings = Main.settings.trackInfoSettings;

            if (car.Bogies == null || car.Bogies.Length == 0) return;
            var bogie = car.Bogies[Math.Min(1, car.Bogies.Length - 1)];
            var track = bogie.track;
            if (track == null)
                return;
            var startSpan = bogie.traveller.Span;
            var controlsOverrider = car.SimController?.controlsOverrider ?? PlayerManager.LastLoco?.SimController?.controlsOverrider;
            var locoDirection = controlsOverrider?.Reverser?.Value >= ReverserControl.NEUTRAL_VALUE;
            var direction = locoDirection == (bogie.TrackDirectionSign > 0);
            var currentGrade = TrackIndexer.Grade(bogie.point1) * (direction ? 1 : -1);

            var events = TrackFollower.FollowTrack(
                track,
                startSpan,
                direction ? trackInfoSettings.maxEventSpan : -trackInfoSettings.maxEventSpan);

            var eventDescriptions = events
                .Concat(OptionalSignalTelemetry.UpcomingSignals)
                .OrderBy(ev => ev.span)
                .ExceptUnnamedTracks()
                .ResolveJunctionSpeedLimits()
                .FilterRedundantSpeedLimits()
                .FilterGradeEvents(currentGrade)
                .TakeWhile(ev => ev.span < trackInfoSettings.maxEventSpan)
                .Take(trackInfoSettings.maxEventCount)
                .Select(ev => ev switch
                    {
                        SignalTrackEvent e => (e.span, SignalDescription(e)),
                        TrackChangeEvent e => (e.span, TrackDescription(e)),
                        JunctionEvent e => (e.span, GetJunctionEventDescription(e, car)),
                        DualSpeedLimitEvent e => (e.span, $"{e.limit} / {e.rightLimit} km/h"),
                        SpeedLimitEvent e => (e.span, GetSpeedLimitEventDescription(e, car)),
                        GradeEvent e => (e.span, $"{e.grade:F1} %"),
                        _ => (0.0, $"Unknown event: {ev}"),
                    })
                .ToList();

            static string FormatSpan((double span, string description) ev)
            {
                var spanQuantity = new Quantity<Dimensions.Length>(ev.span);
                switch (Main.settings.trackInfoSettings.distanceUnits)
                {
                    case Settings.TrackInfoSettings.EventDistanceUnits.ft:
                        return $"{Math.Round(spanQuantity.In(Foot) / 100) * 100:F0} ft";
                    case Settings.TrackInfoSettings.EventDistanceUnits.mi:
                        return $"{spanQuantity.In(Mile):F1} mi";
                    default:
                        return $"{Math.Round(spanQuantity.In(Meter) / 10) * 10:F0} m";
                }
            }

            displayedRows.AddRange(eventDescriptions.Select(ev => (FormatSpan(ev), ev.Item2)));
        }

        /// <summary>Called once during the overlay's Layout pass, before it sizes the panel.</summary>
        public static void PrepareLayout()
        {
            CaptureRows();
            measuredDistanceWidth = 0;
            var descriptionWidth = 0f;
            foreach (var row in displayedRows)
            {
                measuredDistanceWidth = Mathf.Max(measuredDistanceWidth, Styles.TextWidth(row.distance, Styles.rightAlign));
                descriptionWidth = Mathf.Max(descriptionWidth, Styles.TextWidth(row.description, Styles.richText));
            }
            PreferredWidth = RowCount == 0 ? 0 : measuredDistanceWidth + Overlay.ColumnSpacing + descriptionWidth;
        }

        public static void DrawUpcomingEvents(float availableWidth = 390f)
        {
            // PrepareLayout froze these rows; maxEventCount is a cap, not blank padding.
            if (RowCount == 0) return;
            availableWidth = Mathf.Min(availableWidth, PreferredWidth);
            var spacing = Mathf.Min(Overlay.ColumnSpacing, availableWidth * 0.05f);
            var distanceWidth = Mathf.Min(measuredDistanceWidth, Mathf.Max(1, availableWidth - spacing - 1));
            var descriptionWidth = Mathf.Max(1, availableWidth - distanceWidth - spacing);
            GUILayout.BeginHorizontal(GUILayout.Width(availableWidth));
            GUILayout.BeginVertical(GUILayout.Width(distanceWidth));
            for (var row = 0; row < RowCount; row++)
                GUILayout.Label(displayedRows[row].distance, Styles.rightAlign, GUILayout.Width(distanceWidth));
            GUILayout.EndVertical();
            GUILayout.Space(spacing);
            GUILayout.BeginVertical(GUILayout.Width(descriptionWidth));
            for (var row = 0; row < RowCount; row++)
                GUILayout.Label(displayedRows[row].description, Styles.richText, GUILayout.Width(descriptionWidth));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        public static Car? GetCarOnJunction(Junction junction)
        {
            const double SpanTolerance = 7.0;
            var closestDistance = SpanTolerance;
            Car? closest = null;
            foreach (var branch in junction.outBranches.Append(junction.inBranch))
            {
                if (branch?.track == null) continue;
                var length = branch.track.GetKinkedPointSet().span;
                // Native registry observes spawned stock, including AI trains, without any
                // optional mod reference or an obsolete logic-car reverse lookup.
                foreach (var bogie in branch.track.BogiesOnTrack())
                {
                    if (bogie == null || bogie.Car == null || bogie.traveller == null) continue;
                    var distance = branch.first ? bogie.traveller.Span : length - bogie.traveller.Span;
                    if (distance >= 0 && distance < closestDistance)
                    {
                        closestDistance = distance;
                        closest = bogie.Car.logicCar;
                    }
                }
            }
            return closest;
        }
    }
}
