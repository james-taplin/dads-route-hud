using System;
using DvMod.HeadsUpDisplay;

internal static class Program
{
    private static int passed;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        passed++;
    }
    private static void Main()
    {
        // Regression: upstream treated the m/s speed as km/h, underreporting power by 3.6.
        Check(DrivingMath.MechanicalPower(10000f, 10f) == 100000f, "10 kN at 10 m/s must be 100 kW");
        Check(DrivingMath.MechanicalPower(-10000f, -10f) == 100000f, "reverse traction has positive power magnitude");
        Check(DrivingMath.MechanicalPower(10000f, 0f) == 0f, "stall power is zero despite tractive effort");
        Check(DrivingMath.Reverser(0.5f) == "N", "native midpoint is neutral");
        Check(DrivingMath.Reverser(0f) == "R 100 %", "native zero is full reverse");
        Check(DrivingMath.Reverser(1f) == "F 100 %", "native one is full forward");
        Check(DrivingMath.Reverser(0.75f) == "F 50 %", "half forward application");
        Check(DrivingMath.Percent(0.35f) == "35 %", "control application is a percent");
        Check(DrivingMath.Percent(1.2f) == "100 %", "application display clamps upper boundary");
        Check(DrivingMath.Percent(-0.2f) == "0 %", "application display clamps lower boundary");
        Check(DrivingMath.GaugePressure(6f) == 5f, "charged native pipe equals 5 bar gauge");
        Check(DrivingMath.GaugePressure(1f) == 0f, "atmosphere equals zero bar gauge");
        Check(DrivingMath.GaugePressure(4.5f) == 3.5f, "fully applied cylinder equals 3.5 bar gauge");
        Check(!DrivingMath.IsFinite(float.NaN) && !DrivingMath.IsFinite(float.PositiveInfinity), "invalid readings must be unavailable");
        Console.WriteLine(passed + " driving conversion checks passed.");
    }
}
