using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup; }
    public sealed class GameObject { public bool activeInHierarchy = true; }
}
public sealed class RailTrack { }
public sealed class Trainset { public List<TrainCar> cars = new List<TrainCar>(); }
public sealed class Traveller { public double Span; }
public sealed class Bogie { public RailTrack? track; public Traveller? traveller; }
public sealed class TrainCar
{
    public UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    public string ID = "car";
    public bool IsLoco, derailed, Throw;
    public Trainset? trainset;
    public Bogie[] Bogies = Array.Empty<Bogie>();
    public float Speed;
    public double InterCouplerDistance => Throw ? throw new InvalidOperationException("unreadable car") : 20;
    public float GetForwardSpeed() => Speed;
}
public sealed class CarSpawner
{
    public static CarSpawner? Instance;
    public List<TrainCar>? AllCars = new List<TrainCar>();
}
