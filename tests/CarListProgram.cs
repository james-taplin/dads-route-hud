using System;
using System.Globalization;
using System.Linq;
using DvMod.HeadsUpDisplay;
using DV.Logic.Job;
using UnityEngine;

internal static class CarListProgram
{
    private static int assertions;
    private static void Check(bool value, string message)
    { assertions++; if (!value) throw new Exception(message); }

    private static TrainCar MakeCar(string id, float pipe = 6, float reservoir = 5)
    {
        var car = new TrainCar { ID = id };
        car.brakeSystem.brakePipePressure = pipe;
        car.brakeSystem.auxReservoirPressure = reservoir;
        return car;
    }
    private static TrainCar Train(params TrainCar[] cars)
    {
        var train = new Trainset();
        train.cars.AddRange(cars);
        foreach (var car in cars) car.trainset = train;
        return cars[0];
    }
    private static string[] Draw()
    { GUILayout.Labels.Clear(); CarList.DrawCarList(); return GUILayout.Labels.ToArray(); }
    private static string Cell(string[] labels, string heading) => labels[Array.IndexOf(labels, heading) + 1];

    public static int Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Registry.Providers["Front coupler"] = new DataProvider<float>(_ => 10000);
        Registry.Providers["Rear coupler"] = new DataProvider<float>(_ => 20000);
        foreach (var label in new[] { "Brake pipe", "Aux reservoir", "Brake cylinder" }) Registry.Providers[label] = new PressureProvider();
        var a = MakeCar("A", 6, 5);
        var b = MakeCar("B", 3, 6);
        var selected = Train(a, b);
        var task = new Task { destinationTrack = new Track() };
        task.cars.Add(a.logicCar); task.cars.Add(b.logicCar);
        var job = new Job(); job.tasks.Add(task);
        a.logicCar.Job = b.logicCar.Job = job;
        a.rearCoupler!.coupledTo = new Coupler { train = b };
        Time.unscaledTime = 10;
        CarList.Update(selected);
        var beforeDraw = TestNative.Reads;
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        var rows = Draw();
        Check(TestNative.Reads == beforeDraw, "PrepareLayout and Draw must not read native data");
        Check(rows.Contains("1–2") && Cell(rows, "ID") == "A", "Same-job/destination cars stay grouped");
        Check(Cell(rows, "Pipe") == "2.0", "Grouped pipe pressure is the minimum gauge pressure");
        Check(Cell(rows, "Res") == "4.0", "Lower pipe pressure must not overwrite reservoir minimum");
        Check(Cell(rows, "Coupler") == "2", "An uncoupled front must allow the coupled rear lookup");
        CarList.Update(selected);
        Check(TestNative.Reads == beforeDraw, "An unchanged consist honors the refresh cadence");

        var c = Train(MakeCar("C"));
        CarList.Update(c);
        Check(Cell(Draw(), "ID") == "A", "A new Update cannot change rows mid-IMGUI cycle");
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        Check(Cell(Draw(), "ID") == "C", "Changing train refreshes immediately before the cadence expires");
        CarList.Reset();
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        Check(Draw().Length == 0, "Reset removes the prior consist from the next Layout");
        CarList.Update(null);
        CarList.Update(new TrainCar { trainset = new Trainset() });
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        Check(Draw().Length == 0, "Missing/empty consists are safe");

        CarList.Update(c);
        DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings.groupCarsByJob = false;
        var beforeGrouping = TestNative.Reads;
        CarList.Update(c);
        Check(TestNative.Reads > beforeGrouping, "Grouping changes refresh immediately");
        CarList.Update(selected);
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        Check(!Draw().Contains("1–2") && Draw().Contains("B"), "Individual mode retains each car as its own row");
        a.frontCoupler = a.rearCoupler = b.frontCoupler = b.rearCoupler = null;
        Time.unscaledTime += 1;
        CarList.Update(selected);
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        Check(Cell(Draw(), "Coupler") == " ", "Missing coupling links safely produce an unavailable stress cell");

        // Passenger Jobs 5.3.1 RuralTaskData has an extended task type and no
        // nestedTasks. Destination discovery must follow the native data shape.
        var passenger = Train(MakeCar("Passenger"));
        var passengerPlatform = new Track();
        passengerPlatform.ID.FullDisplayID = "Passenger platform";
        var passengerLeaf = new Task { type = (TaskType)42, nestedTasks = null, destinationTrack = passengerPlatform };
        passengerLeaf.cars.Add(passenger.logicCar);
        var passengerSequence = new Task { type = TaskType.Other };
        passengerSequence.nestedTasks!.Add(passengerLeaf);
        var passengerJob = new Job();
        passengerJob.tasks.Add(passengerSequence);
        passenger.logicCar.Job = passengerJob;
        CarList.Update(passenger);
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        Check(Cell(Draw(), "Destination") == "Passenger platform", "An active custom loading leaf preserves its actual destination without traversing null children");
        passengerLeaf.state = TaskState.Other;
        Time.unscaledTime += 1;
        CarList.Update(passenger);
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        Check(Cell(Draw(), "Destination") == " ", "An inactive custom loading leaf does not claim a destination");
        passengerLeaf.state = TaskState.InProgress;
        passengerLeaf.cars.Clear();
        Time.unscaledTime += 1;
        CarList.Update(passenger);
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        Check(Cell(Draw(), "Destination") == " ", "A loading leaf for another car does not claim this passenger car");
        var freightDestination = new Track();
        freightDestination.ID.FullDisplayID = "Freight destination";
        var freightTask = new Task { destinationTrack = freightDestination };
        freightTask.cars.Add(passenger.logicCar);
        passengerSequence.nestedTasks.Add(freightTask);
        Time.unscaledTime += 1;
        CarList.Update(passenger);
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        Check(Cell(Draw(), "Destination") == "Freight destination", "A nonmatching custom leaf permits the following native transport task");
        passengerLeaf.cars.Add(passenger.logicCar);
        passengerLeaf.destinationTrack = null;
        Time.unscaledTime += 1;
        CarList.Update(passenger);
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        Check(Cell(Draw(), "Destination") == "Freight destination", "A custom leaf without a destination preserves later destination lookup");
        passengerLeaf.cars = null!;
        Time.unscaledTime += 1;
        CarList.Update(passenger);
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        Check(Cell(Draw(), "Destination") == "Freight destination", "A leaf without cars safely permits a later matching task");

        // A car-details source failure must return control to the caller so the
        // shared Update callback can continue collecting the route.
        var samplingSettings = DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings;
        samplingSettings.updatePeriod = 0.75f;
        freightTask.ReadFailure = new InvalidOperationException("test task data unavailable");
        Time.unscaledTime += 1;
        var warningCount = DvMod.HeadsUpDisplay.Main.mod!.Logger.Warnings.Count;
        Exception? escaped = null;
        try { CarList.Update(passenger); }
        catch (Exception exception) { escaped = exception; }
        Check(escaped == null, "A task-data sampling failure returns normally to the shared Update caller");
        Check(Cell(Draw(), "Destination") == "Freight destination", "Failure does not replace displayed rows mid-IMGUI cycle");
        CarList.PrepareLayout(samplingSettings);
        Check(Draw().Length == 0, "The next Layout clears stale rows after failed sampling");
        var failedAttempts = freightTask.ReadAttempts;
        var failedReads = TestNative.Reads;
        Time.unscaledTime += 0.5f;
        CarList.Update(passenger);
        Check(freightTask.ReadAttempts == failedAttempts && TestNative.Reads == failedReads, "A failed sample honors the configured retry cadence without native reads");
        Time.unscaledTime += 0.25f;
        CarList.Update(passenger);
        Check(freightTask.ReadAttempts == failedAttempts + 1, "A failed sample retries when the configured cadence expires");
        Check(DvMod.HeadsUpDisplay.Main.mod.Logger.Warnings.Count == warningCount + 1, "Repeated identical sampling failures emit one warning");
        freightTask.ReadFailure = null;
        Time.unscaledTime += 0.75f;
        CarList.Update(passenger);
        CarList.PrepareLayout(samplingSettings);
        Check(Cell(Draw(), "Destination") == "Freight destination", "A later successful sample restores the car details");

        samplingSettings.updatePeriod = 0;
        freightTask.ReadFailure = new InvalidOperationException("test second task failure");
        CarList.Update(passenger);
        failedAttempts = freightTask.ReadAttempts;
        failedReads = TestNative.Reads;
        Time.unscaledTime += 0.125f;
        CarList.Update(passenger);
        Check(freightTask.ReadAttempts == failedAttempts && TestNative.Reads == failedReads, "A zero update period still bounds failed sampling retries");
        Time.unscaledTime += 0.125f;
        CarList.Update(passenger);
        Check(freightTask.ReadAttempts == failedAttempts + 1, "A zero-period failed sample retries after the minimum quarter second");
        Check(DvMod.HeadsUpDisplay.Main.mod.Logger.Warnings.Count == warningCount + 2, "A distinct sampling failure emits its own single warning");
        var healthy = Train(MakeCar("Healthy"));
        CarList.Update(healthy);
        CarList.PrepareLayout(samplingSettings);
        Check(Cell(Draw(), "ID") == "Healthy", "Changing the train bypasses a failed sample's retry delay immediately");
        samplingSettings.updatePeriod = 0.25f;

        var d = MakeCar("D"); var e = MakeCar("E");
        var reorder = Train(MakeCar("First"), d, e, MakeCar("Last"));
        CarList.Update(reorder);
        var beforeReorder = TestNative.Reads;
        reorder.trainset!.cars[1] = e; reorder.trainset.cars[2] = d;
        CarList.Update(reorder);
        Check(TestNative.Reads == beforeReorder, "Internal reordering remains bounded by the configured cadence");
        Time.unscaledTime += 1;
        CarList.Update(reorder);
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        var reorderedRows = Draw();
        Check(Array.IndexOf(reorderedRows, "E") < Array.IndexOf(reorderedRows, "D"), "The next sample reflects equal-count internal reordering");
        DvMod.HeadsUpDisplay.Main.settings.layoutSettings.showHud = false;
        var beforeHidden = TestNative.Reads;
        CarList.Update(c);
        CarList.PrepareLayout(DvMod.HeadsUpDisplay.Main.settings.trainInfoSettings);
        Check(Draw().Length == 0 && TestNative.Reads == beforeHidden, "A hidden HUD stops collection and clears rows");
        Console.WriteLine($"{assertions} production CarList regression checks passed.");
        return 0;
    }
}
