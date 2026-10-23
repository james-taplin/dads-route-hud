using System;
using System.Collections.Generic;
using System.Linq;
using DV;
using DV.Logic.Job;
using DV.MultipleUnit;
using DV.Simulation.Brake;
using DV.ThingTypes;
using DV.Utils;
using QuantitiesNet;
using UnityEngine;
using static QuantitiesNet.Quantities;

namespace DvMod.HeadsUpDisplay
{
    public static class CarList
    {
        private readonly struct CarGroup
        {
            public readonly int startIndex;
            public readonly int endIndex;
            public readonly List<TrainCar> cars;
            public readonly float maxStress;
            public readonly float? maxCouplerStress;
            public readonly Job? job;
            public readonly Track? nextDestination;

            public readonly Pressure minBrakePipePressure;
            public readonly Pressure minBrakeReservoirPressure;
            public readonly Pressure maxBrakeCylinderPressure;
            public readonly float maxBrakeFactor;

            public CarGroup(
                int startIndex,
                int endIndex,
                List<TrainCar> cars,
                float maxStress,
                float? maxCouplerStress,
                Job? job,
                Track? nextDestination,
                Pressure minBrakePipePressure,
                Pressure minBrakeReservoirPressure,
                Pressure maxBrakeCylinderPressure,
                float maxBrakeFactor)
            {
                this.startIndex = startIndex;
                this.endIndex = endIndex;
                this.cars = cars;
                this.maxStress = maxStress;
                this.maxCouplerStress = maxCouplerStress;
                this.job = job;
                this.nextDestination = nextDestination;
                this.minBrakePipePressure = minBrakePipePressure;
                this.minBrakeReservoirPressure = minBrakeReservoirPressure;
                this.maxBrakeCylinderPressure = maxBrakeCylinderPressure;
                this.maxBrakeFactor = maxBrakeFactor;
            }
        }

        private static float? GetCouplerStress(IReadOnlyList<TrainCar> cars, int index)
        {
            if (index == 0)
                return null;
            if (!(Registry.GetProvider("Front coupler") is DataProvider<float> frontCouplerProvider))
                return null;
            if (!(Registry.GetProvider("Rear coupler") is DataProvider<float> rearCouplerProvider))
                return null;

            var frontCar = cars[index - 1];
            var rearCar = cars[index];
            static bool IsFrontCoupledTo(TrainCar car, TrainCar attached) =>
                car.frontCoupler?.coupledTo?.train == attached;
            static bool IsRearCoupledTo(TrainCar car, TrainCar attached) =>
                car.rearCoupler?.coupledTo?.train == attached;

            (TrainCar? car, bool isFront) FindCoupler()
            {
                if (IsFrontCoupledTo(frontCar, rearCar))
                    return (frontCar, true);
                else if (IsRearCoupledTo(frontCar, rearCar))
                    return (frontCar, false);
                else if (IsFrontCoupledTo(rearCar, frontCar))
                    return (rearCar, true);
                else if (IsRearCoupledTo(rearCar, frontCar))
                    return (rearCar, false);
                // A consist can be observed between native coupling/uncoupling updates.
                return (null, false);
            }

            var (carWithCoupler, isFront) = FindCoupler();
            if (carWithCoupler == null) return null;
            var provider = isFront ? frontCouplerProvider : rearCouplerProvider;
            if (provider.TryGetValue(carWithCoupler, out var stress))
                return stress;
            return null;
        }

        private static Pressure GetAuxReservoirPressure(TrainCar car)
        {
            return new Pressure(DrivingMath.GaugePressure(car.IsLoco
                ? car.brakeSystem.mainReservoirPressure
                : car.brakeSystem.auxReservoirPressure), QuantitiesNet.Units.Bar);
        }

        private static Pressure GetBrakeCylinderPressure(TrainCar car)
        {
            return new Pressure(DrivingMath.GaugePressure(car.brakeSystem.brakeCylinderPressure), QuantitiesNet.Units.Bar);
        }

        private static IEnumerable<CarGroup> GetCarGroups(IReadOnlyList<TrainCar> cars, bool individual)
        {
            if (cars.Count == 0) yield break;
            Job? prevJob = null;
            Track? prevDestTrack = null;
            int startIndex = 0;
            float maxStress = 0f;
            float? maxCouplerStress = null;

            var firstCar = cars[0];
            var groupCars = new List<TrainCar>() { firstCar };
            Pressure minBrakePipePressure = new Pressure(DrivingMath.GaugePressure(firstCar.brakeSystem.brakePipePressure), QuantitiesNet.Units.Bar);
            Pressure minBrakeReservoirPressure = GetAuxReservoirPressure(firstCar);
            Pressure maxBrakeCylinderPressure = GetBrakeCylinderPressure(firstCar);
            float maxBrakeFactor = firstCar.brakeSystem.brakingFactor;

            int i = 0;
            for (var index = 0; index < cars.Count; index++)
            {
                var car = cars[index];
                float carStress = car.GetComponent<TrainStress>()?.derailBuildUp ?? 0f;
                float? couplerStress = GetCouplerStress(cars, index);
                Job? job = SingletonBehaviour<JobsManager>.Instance?.GetJobOfCar(car.logicCar);
                Track? nextDestination = GetNextDestinationTrack(job, car.logicCar);
                BrakeSystem brakeSystem = car.brakeSystem;
                Pressure pipePressure = new Pressure(DrivingMath.GaugePressure(brakeSystem.brakePipePressure), QuantitiesNet.Units.Bar);
                Pressure auxReservoirPressure = GetAuxReservoirPressure(car);
                Pressure brakeCylinderPressure = GetBrakeCylinderPressure(car);

                if (individual || nextDestination == null || nextDestination != prevDestTrack || job != prevJob)
                {
                    // complete previous group
                    if (i > 0)
                    {
                        yield return new CarGroup(
                            startIndex,
                            i,
                            groupCars,
                            maxStress,
                            maxCouplerStress,
                            prevJob,
                            prevDestTrack,
                            minBrakePipePressure,
                            minBrakeReservoirPressure,
                            maxBrakeCylinderPressure,
                            maxBrakeFactor);
                    }

                    // start new group
                    startIndex = i;
                    groupCars = new List<TrainCar>() { car };
                    prevJob = job;
                    prevDestTrack = nextDestination;
                    maxStress = carStress;
                    maxCouplerStress = couplerStress;
                    minBrakePipePressure = pipePressure;
                    minBrakeReservoirPressure = auxReservoirPressure;
                    maxBrakeCylinderPressure = brakeCylinderPressure;
                    maxBrakeFactor = brakeSystem.brakingFactor;
                }
                else
                {
                    groupCars.Add(car);
                    if (carStress > maxStress)
                        maxStress = carStress;
                    if (couplerStress != null && (maxCouplerStress == null || couplerStress > maxCouplerStress))
                        maxCouplerStress = couplerStress;
                    if (pipePressure < minBrakePipePressure)
                        minBrakePipePressure = pipePressure;
                    if (auxReservoirPressure < minBrakeReservoirPressure)
                        minBrakeReservoirPressure = auxReservoirPressure;
                    if (brakeCylinderPressure > maxBrakeCylinderPressure)
                        maxBrakeCylinderPressure = brakeCylinderPressure;
                    if (brakeSystem.brakingFactor > maxBrakeFactor)
                        maxBrakeFactor = brakeSystem.brakingFactor;
                }
                i++;
            }

            // complete last group
            yield return new CarGroup(
                startIndex,
                i,
                groupCars,
                maxStress,
                maxCouplerStress,
                prevJob,
                prevDestTrack,
                minBrakePipePressure,
                minBrakeReservoirPressure,
                maxBrakeCylinderPressure,
                maxBrakeFactor);
        }

        private const char EnDash = '\u2013';
        private static float lastUpdateTime;
        private static Trainset? sourceTrain;
        private static TrainCar? sourceFirst, sourceLast;
        private static int sourceCount;
        private static bool sourceReversed, sourceGrouped;
        private static bool sampleFailed;
        private static string? lastFailure;
        private static CarRow[] currentRows = Array.Empty<CarRow>();
        private static CarRow[] displayedRows = Array.Empty<CarRow>();
        private static bool showStress, showCoupler, showJobs, showDestinations, showBrakes;

        /// <summary>A fully sampled row: drawing never dereferences live cars, jobs or tracks.</summary>
        private sealed class CarRow
        {
            public string Range = "", Id = "", Stress = "", Coupler = "", Job = "", Destination = "";
            public string Pipe = "", Reservoir = "", Cylinder = "", BrakeForce = "";
            public Color IdColor, StressColor, CouplerColor, JobColor, DestinationColor;
        }

        /// <summary>Called by the mod's Update callback, outside IMGUI and at the saved cadence.</summary>
        public static void Update(TrainCar? selectedCar)
        {
            var settings = Main.settings.trainInfoSettings;
            var train = selectedCar?.trainset;
            if (!Main.settings.layoutSettings.showHud || !settings.enabled || !settings.showCarList
                || train == null || train.cars.Count == 0)
            {
                Reset();
                return;
            }
            var first = train.cars[0];
            var last = train.cars[train.cars.Count - 1];
            var reversed = last == PlayerManager.LastLoco || (!first.IsLoco && last.IsLoco);
            var now = UnityEngine.Time.unscaledTime;
            if (ReferenceEquals(sourceTrain, train) && sourceFirst == first && sourceLast == last
                && sourceCount == train.cars.Count && sourceReversed == reversed
                && sourceGrouped == settings.groupCarsByJob && now >= lastUpdateTime
                && now - lastUpdateTime < (sampleFailed ? Math.Max(0.25f, settings.updatePeriod) : settings.updatePeriod)) return;
            // Remember failed attempts too, so unavailable native data cannot force a retry every frame.
            sourceTrain = train; sourceFirst = first; sourceLast = last;
            sourceCount = train.cars.Count; sourceReversed = reversed; sourceGrouped = settings.groupCarsByJob;
            lastUpdateTime = now;
            try
            {
                var cars = train.cars.ToArray();
                if (reversed) Array.Reverse(cars);
                var pipe = MakePressureFormatter("Brake pipe");
                var reservoir = MakePressureFormatter("Aux reservoir");
                var cylinder = MakePressureFormatter("Brake cylinder");
                currentRows = GetCarGroups(cars, !settings.groupCarsByJob)
                    .Select(group => CaptureRow(group, pipe, reservoir, cylinder)).ToArray();
                sampleFailed = false;
                lastFailure = null;
            }
            catch (Exception exception)
            {
                // Train details must not abort the shared Update before route collection can run.
                currentRows = Array.Empty<CarRow>();
                sampleFailed = true;
                var failure = exception.GetType().Name + ": " + exception.Message;
                if (failure != lastFailure)
                {
                    lastFailure = failure;
                    Main.mod?.Logger.Warning("Car list sampling failed; route collection will continue: " + failure);
                }
            }
        }

        public static void Reset()
        {
            sourceTrain = null; sourceFirst = sourceLast = null; sourceCount = 0;
            currentRows = Array.Empty<CarRow>();
            sampleFailed = false;
            lastFailure = null;
        }

        /// <summary>Freeze the row array and columns together for Layout through Repaint.</summary>
        public static void PrepareLayout(Settings.TrainInfoSettings settings)
        {
            displayedRows = currentRows;
            showStress = settings.showCarStress;
            showCoupler = showStress && Registry.GetProvider("Front coupler") != null;
            showJobs = settings.showCarJobs;
            showDestinations = settings.showCarDestinations;
            showBrakes = settings.showCarBrakeStatus;
        }

        private static CarRow CaptureRow(CarGroup group, Func<Pressure, string> pipe,
            Func<Pressure, string> reservoir, Func<Pressure, string> cylinder)
        {
            var stress = group.maxStress / Globals.G.GameParams.DerailBuildUpThreshold * 100;
            var coupler = group.maxCouplerStress / 1e4f;
            var carsAtDestination = group.cars.Count(car => car.logicCar.CurrentTrack == group.nextDestination);
            return new CarRow
            {
                Range = group.startIndex + 1 == group.endIndex ? group.endIndex.ToString() : $"{group.startIndex + 1}{EnDash}{group.endIndex}",
                Id = group.cars[0].ID, IdColor = GetCarColor(group.cars[0]),
                Stress = stress.ToString("F0"), StressColor = StressColor(stress),
                Coupler = coupler?.ToString("F0") ?? " ", CouplerColor = coupler.HasValue ? StressColor(coupler.Value) : Color.white,
                Job = group.job?.ID ?? " ", JobColor = JobColor(group.job),
                Destination = group.nextDestination?.ID?.FullDisplayID ?? " ",
                DestinationColor = carsAtDestination == group.cars.Count ? Color.green : carsAtDestination > 0 ? Color.yellow : Color.white,
                Pipe = pipe(group.minBrakePipePressure), Reservoir = reservoir(group.minBrakeReservoirPressure),
                Cylinder = cylinder(group.maxBrakeCylinderPressure), BrakeForce = group.maxBrakeFactor.ToString("P0")
            };
        }

        private static Color StressColor(float percent) => Color.HSVToRGB(Mathf.Lerp(1f / 3f, 0, (percent - 30f) / 40f), 1f, 1f);

        public static void DrawCarList()
        {
            if (displayedRows.Length == 0) return;
            var savedColor = GUI.contentColor;
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                GUILayout.Label(" ", Styles.noWrap);
                foreach (var row in displayedRows) GUILayout.Label(row.Range, Styles.noWrap);
                GUILayout.EndVertical();
                DrawColumn("ID", row => row.Id, row => row.IdColor, false);
                if (showStress) DrawColumn("Derail", row => row.Stress, row => row.StressColor, rightAlign: true);
                if (showCoupler) DrawColumn("Coupler", row => row.Coupler, row => row.CouplerColor, rightAlign: true);
                if (showJobs) DrawColumn("Job", row => row.Job, row => row.JobColor);
                if (showDestinations) DrawColumn("Destination", row => row.Destination, row => row.DestinationColor);
                if (showBrakes)
                {
                    DrawColumn("Pipe", row => row.Pipe);
                    DrawColumn("Res", row => row.Reservoir);
                    DrawColumn("Cyl", row => row.Cylinder);
                    DrawColumn("Force", row => row.BrakeForce, rightAlign: true);
                }
                GUILayout.EndHorizontal();
            }
            finally { GUI.contentColor = savedColor; }
        }

        private static void DrawColumn(string heading, Func<CarRow, string> value,
            Func<CarRow, Color>? color = null, bool space = true, bool rightAlign = false)
        {
            if (space) GUILayout.Space(Overlay.ColumnSpacing);
            var style = rightAlign ? Styles.rightAlign : Styles.noWrap;
            GUILayout.BeginVertical();
            GUILayout.Label(heading, Styles.noWrap);
            var saved = GUI.contentColor;
            foreach (var row in displayedRows)
            {
                GUI.contentColor = color?.Invoke(row) ?? saved;
                GUILayout.Label(value(row), style);
            }
            GUI.contentColor = saved;
            GUILayout.EndVertical();
        }

        private const float HueOrange = 30f / 360f;
        private static Color GetCarColor(TrainCar car)
        {
            if (car.derailed)
                return Color.red;
            if (!car.IsLoco)
                return Color.white;

            var isMultipleUnitCapable = car.TryGetComponent<MultipleUnitModule>(out var muModule);
            var frontMUDisconnected = isMultipleUnitCapable
                && car.frontCoupler?.coupledTo?.train?.carType == car.carType
                && !muModule.FrontCable.IsConnected;
            var rearMUDisconnected = isMultipleUnitCapable
                && car.rearCoupler?.coupledTo?.train?.carType == car.carType
                && !muModule.RearCable.IsConnected;
            var hasDisconnectedMUCable = frontMUDisconnected || rearMUDisconnected;
            var isRunning = car.SimController?.controlsOverrider?.EngineOnReader?.IsOn ?? true;

            return Color.HSVToRGB(HueOrange, hasDisconnectedMUCable ? 1 : 0, isRunning ? 1 : 0.5f);
        }

        private static Color JobColor(Job? job)
        {
            if (job == null)
                return Color.white;
            return job.State switch
            {
                JobState.Available => Color.yellow,
                JobState.InProgress => Color.white,
                JobState.Completed => Color.green,
                _ => Color.red,
            };
        }

        private static Track? GetNextDestinationTrack(Task task, Car car)
        {
            var data = task.GetTaskData();
            // Optional job types can supply a destination leaf without a child-task list.
            if (data.type == TaskType.Transport || data.type == TaskType.Warehouse || data.nestedTasks == null)
                return (data.state == TaskState.InProgress && data.cars != null && data.cars.Contains(car)) ? data.destinationTrack : null;
            return data.nestedTasks.Select(t => GetNextDestinationTrack(t, car)).FirstOrDefault(track => track != null);
        }

        private static Track? GetNextDestinationTrack(Job? job, Car car)
        {
            return job?.tasks.Select(task => GetNextDestinationTrack(task, car)).FirstOrDefault(track => track != null);
        }

        private static Func<Pressure, string> MakePressureFormatter(string providerLabel)
        {
            var provider = Registry.GetProvider(providerLabel);
            if (provider is IQuantityProvider quantityProvider)
            {
                var providerSettings = Main.settings.drivingInfoSettings.GetProviderSettings(quantityProvider);
                if (!providerSettings.TryGetUnit(quantityProvider.Dimension, out var unit))
                    return pressure => pressure.In(QuantitiesNet.Units.Bar).ToString("F1");
                var format = $"F{providerSettings.precision}";
                return pressure => pressure.In(unit).ToString(format);
            }
            return pressure => $"{pressure.Scalar:F2}";
        }

    }
}
