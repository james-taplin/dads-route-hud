using System;
using DV.Simulation.Brake;
using QuantitiesNet;
using UnityEngine;
using static QuantitiesNet.Units;

namespace DvMod.HeadsUpDisplay
{
    public static class GeneralProviders
    {
        public static void Register()
        {
            Registry.Register(new QuantityQueryDataProvider<Dimensions.Length>("Altitude",
                car => new Quantities.Length(car.transform.position.y - WorldMover.currentMove.y - 110f)));
            Registry.Register(new QuantityQueryDataProvider<Dimensions.Velocity>("Speed",
                car => new Quantities.Velocity(Mathf.Abs(car.GetForwardSpeed()), MetersPerSecond)));
            Registry.Register(new FloatQueryDataProvider("Acceleration", NativeTelemetry.Acceleration,
                value => DrivingMath.Number(value, 2, "m/s²")));
            Registry.Register(new FloatQueryDataProvider("Grade", car =>
            {
                var angle = car.transform.localEulerAngles.x;
                angle = angle > 180 ? 360f - angle : -angle;
                return Mathf.Tan(angle * Mathf.PI / 180) * 100f;
            }, value => value.ToString("+0.0' %';−0.0' %';0.0' %'")));

            Pressure("Brake pipe", brake => brake.brakePipePressure);
            Pressure("Brake cylinder", brake => brake.brakeCylinderPressure);
            Pressure("Main reservoir", brake => brake.mainReservoirPressure);
            Pressure("Aux reservoir", brake => brake.auxReservoirPressure);
            Registry.Register(new FloatQueryDataProvider("Brake application", car => car.brakeSystem?.brakingFactor, DrivingMath.Percent));
            Registry.Register(new FloatQueryDataProvider("Cylinder application", car => car.brakeSystem?.BrakeCylinderPressureNormalized, DrivingMath.Percent));
            Registry.Register(new FloatQueryDataProvider("Brake temperature", car => car.brakeSystem?.heatController?.temperature,
                value => DrivingMath.Number(value, 0, "°C")));
            Registry.Register(new FloatQueryDataProvider("Brake overheating", car => car.brakeSystem?.heatController?.overheatPercentage, DrivingMath.Percent));
            Registry.Register(new QuantityQueryDataProvider<Dimensions.Mass>("Vehicle mass", car =>
                car.massController == null ? null : new Quantities.Mass(car.massController.TotalMass, Kilogram)));
            Registry.Register(new FloatQueryDataProvider("Kinetic energy", car =>
            {
                if (car.massController == null) return null;
                var speed = car.GetForwardSpeed();
                return 0.5f * car.massController.TotalMass * speed * speed / 1000000f;
            }, value => DrivingMath.Number(value, 2, "MJ")));
            Registry.Register(new FloatQueryDataProvider("Grade resistance", car =>
            {
                if (car.massController == null) return null;
                return car.massController.TotalMass * 9.80665f * car.transform.forward.y / 1000f;
            }, value => DrivingMath.Number(value, 1, "kN")));
        }

        private static void Pressure(string label, Func<BrakeSystem, float> read)
        {
            Registry.Register(new QuantityQueryDataProvider<Dimensions.Pressure>(label, car =>
                car.brakeSystem == null ? null : new Quantities.Pressure(DrivingMath.GaugePressure(read(car.brakeSystem)), Bar)));
        }
    }
}
