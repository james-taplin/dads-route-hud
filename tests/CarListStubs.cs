// Native/UI boundary doubles; compile the production CarList and actual QuantitiesNet arithmetic.
using System;
using System.Collections.Generic;
using QuantitiesNet;
using DV.ThingTypes;

public static class TestNative { public static int Reads; }
namespace UnityEngine
{
    public static class Time { public static float unscaledTime; }
    public struct Color
    {
        public static Color white => default;
        public static Color red => default;
        public static Color green => default;
        public static Color yellow => default;
        public static Color HSVToRGB(float h, float s, float v) => default;
    }
    public static class Mathf { public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0, Math.Min(1, t)); }
    public sealed class GUIStyle { }
    public static class GUI { public static Color contentColor; }
    public static class GUILayout
    {
        public static readonly List<string> Labels = new List<string>();
        public static void BeginHorizontal() { }
        public static void EndHorizontal() { }
        public static void BeginVertical() { }
        public static void EndVertical() { }
        public static void Label(string value, GUIStyle style) => Labels.Add(value);
        public static void Space(float value) { }
    }
}
namespace DV
{
    public static class Globals { public static Game G = new Game(); }
    public sealed class Game { public GameParams GameParams = new GameParams(); }
    public sealed class GameParams { public float DerailBuildUpThreshold = 100; }
}
namespace DV.Utils { public static class SingletonBehaviour<T> where T : new() { public static T Instance = new T(); } }
namespace DV.ThingTypes { public enum JobState { Available, InProgress, Completed } }
namespace DV.Simulation.Brake
{
    public sealed class BrakeSystem
    {
        public float brakePipePressure = 6, mainReservoirPressure = 7, auxReservoirPressure = 5;
        public float brakeCylinderPressure = 1, brakingFactor;
    }
}
namespace DV.MultipleUnit
{
    public sealed class MultipleUnitModule { public Cable FrontCable = new Cable(), RearCable = new Cable(); }
    public sealed class Cable { public bool IsConnected = true; }
}
namespace DV.Logic.Job
{
    public sealed class Car { public Track? CurrentTrack; public Job? Job; }
    public sealed class Track { public TrackId ID = new TrackId(); }
    public sealed class TrackId { public string FullDisplayID = "Destination"; }
    public enum TaskType { Transport, Warehouse, Other }
    public enum TaskState { InProgress, Other }
    public sealed class Task
    {
        public TaskType type = TaskType.Transport;
        public TaskState state = TaskState.InProgress;
        public List<Car> cars = new List<Car>();
        public Track? destinationTrack;
        public List<Task> nestedTasks = new List<Task>();
        public Task GetTaskData() { TestNative.Reads++; return this; }
    }
    public sealed class Job { public string ID = "Job"; public JobState State; public List<Task> tasks = new List<Task>(); }
    public sealed class JobsManager { public Job? GetJobOfCar(Car car) { TestNative.Reads++; return car.Job; } }
}
public sealed class Trainset { public List<TrainCar> cars = new List<TrainCar>(); }
public sealed class TrainCar
{
    public string ID = "Car", carType = "Wagon";
    public bool IsLoco, derailed;
    public Trainset? trainset;
    public Coupler? frontCoupler = new Coupler(), rearCoupler = new Coupler();
    private readonly DV.Simulation.Brake.BrakeSystem brake = new DV.Simulation.Brake.BrakeSystem();
    public DV.Simulation.Brake.BrakeSystem brakeSystem { get { TestNative.Reads++; return brake; } }
    public DV.Logic.Job.Car logicCar = new DV.Logic.Job.Car();
    public SimController SimController = new SimController();
    public T? GetComponent<T>() where T : class { TestNative.Reads++; return new TrainStress() as T; }
    public bool TryGetComponent<T>(out T value) { TestNative.Reads++; value = default!; return false; }
}
public sealed class Coupler { public Coupler? coupledTo; public TrainCar? train; }
public sealed class TrainStress { public float derailBuildUp; }
public sealed class SimController { public Controls controlsOverrider = new Controls(); }
public sealed class Controls { public Engine EngineOnReader = new Engine(); }
public sealed class Engine { public bool IsOn = true; }
public static class PlayerManager { public static TrainCar? LastLoco; }
namespace DvMod.HeadsUpDisplay
{
    public static class Main { public static Settings settings = new Settings(); }
    public sealed class Settings
    {
        public TrainInfoSettings trainInfoSettings = new TrainInfoSettings();
        public LayoutSettings layoutSettings = new LayoutSettings();
        public DrivingInfoSettings drivingInfoSettings = new DrivingInfoSettings();
        public sealed class TrainInfoSettings
        {
            public bool enabled = true, showCarList = true, groupCarsByJob = true;
            public bool showCarStress = true, showCarJobs = true, showCarDestinations = true, showCarBrakeStatus = true;
            public float updatePeriod = 0.25f;
        }
        public sealed class LayoutSettings { public bool showHud = true; }
        public sealed class DrivingInfoSettings
        {
            public ProviderSettings GetProviderSettings(IQuantityProvider provider) => new ProviderSettings();
            public sealed class ProviderSettings
            {
                public int precision = 1;
                public bool TryGetUnit(Dimension dimension, out Unit unit) { unit = Units.Bar; return true; }
            }
        }
    }
    public interface IDataProvider { }
    public interface IQuantityProvider : IDataProvider { Dimension Dimension { get; } }
    public sealed class PressureProvider : IQuantityProvider { public Dimension Dimension => Dimensions.Pressure.dimension; }
    public sealed class DataProvider<T> : IDataProvider
    {
        private readonly Func<TrainCar, T> read;
        public DataProvider(Func<TrainCar, T> read) { this.read = read; }
        public bool TryGetValue(TrainCar car, out T value) { TestNative.Reads++; value = read(car); return true; }
    }
    public static class Registry
    {
        public static readonly Dictionary<string, IDataProvider> Providers = new Dictionary<string, IDataProvider>();
        public static IDataProvider? GetProvider(string label) => Providers.TryGetValue(label, out var provider) ? provider : null;
    }
    public static class Styles { public static readonly UnityEngine.GUIStyle noWrap = new UnityEngine.GUIStyle(), rightAlign = new UnityEngine.GUIStyle(); }
    public static class Overlay { public const int ColumnSpacing = 6; }
}
