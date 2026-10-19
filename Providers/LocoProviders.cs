using System;
using QuantitiesNet;
using static QuantitiesNet.Quantities;
using static QuantitiesNet.Units;

namespace DvMod.HeadsUpDisplay
{
    internal static class LocoProviders
    {
        public static void Register()
        {
            Registry.Register(new QuantityQueryDataProvider<Dimensions.Force>("Tractive effort", car =>
            {
                var driving = car.SimController?.drivingForce;
                return driving == null ? null : new Force(driving.generatedForce, Newton);
            }));
            Registry.Register(new QuantityQueryDataProvider<Dimensions.Power>("Power", car =>
            {
                var driving = car.SimController?.drivingForce;
                // GetForwardSpeed is m/s. This is requested mechanical power, before adhesion limits.
                return driving == null ? null : new Power(DrivingMath.MechanicalPower(driving.generatedForce, car.GetForwardSpeed()), Watt);
            }));
            Registry.Register(new QuantityQueryDataProvider<Dimensions.Force>("Adhesion", car =>
            {
                var wheels = car.SimController?.wheelslipController;
                return wheels == null ? null : new Force(wheels.TotalForceLimit, Newton);
            }));
            Registry.Register(new FloatQueryDataProvider("Slip", car => car.SimController?.wheelslipController?.wheelslip, DrivingMath.Percent));
            Registry.Register(new FloatQueryDataProvider("Adhesion demand", car =>
            {
                var sim = car.SimController;
                if (sim?.drivingForce == null || sim.wheelslipController == null || sim.wheelslipController.TotalForceLimit <= 0f) return null;
                return Math.Abs(sim.drivingForce.generatedForce) / sim.wheelslipController.TotalForceLimit;
            }, value => DrivingMath.Number(value * 100f, 0, "%")));
        }
    }
}
