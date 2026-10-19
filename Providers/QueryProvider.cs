using QuantitiesNet;
using System;
using System.Linq;

namespace DvMod.HeadsUpDisplay
{
    public class FloatQueryDataProvider : DataProvider<float>
    {
        private readonly Func<TrainCar, float?> provider;
        private readonly Func<float, string> formatter;
        private bool loggedFailure;

        public FloatQueryDataProvider(string label, Func<TrainCar, float?> provider, Func<float, string> formatter, IComparable? order = null, bool hidden = false)
        : base(label, order, hidden)
        {
            this.provider = provider;
            this.formatter = formatter;
        }

        public override bool TryGetFormatted(TrainCar car, out string s)
        {
            if (TryGetValue(car, out var v))
            {
                s = formatter(v);
                return true;
            }
            s = "";
            return false;
        }

        public override bool TryGetValue(TrainCar car, out float v)
        {
            float? result = null;
            try { result = provider(car); }
            catch (Exception exception)
            {
                if (!loggedFailure)
                {
                    Main.mod?.Logger.Warning(Label + " unavailable: " + exception.Message);
                    loggedFailure = true;
                }
            }
            if (!result.HasValue || !DrivingMath.IsFinite(result.Value))
            {
                v = default;
                return false;
            }
            v = (float)result;
            return true;
        }
    }

    public class QuantityQueryDataProvider<D> : DataProvider<Quantity<D>>, IQuantityProvider
    where D : IDimension, new()
    {
        private readonly Func<TrainCar, Quantity<D>?> provider;
        private bool loggedFailure;

        public QuantityQueryDataProvider(
            string label,
            Func<TrainCar, Quantity<D>?> provider,
            IComparable? order = null,
            bool hidden = false) : base(label, order, hidden)
        {
            this.provider = provider;
        }

        public Dimension Dimension => Dimension.ForType<D>();

        public override bool TryGetFormatted(TrainCar car, out string s)
        {
            if (TryGetValue(car, out var v))
            {
                var settings = Main.settings.drivingInfoSettings.GetProviderSettings(this);
                if (settings.TryGetUnit(Dimension, out var unit))
                {
                    var format = $"F{settings.precision}";
                    var strValue = v.In(unit).ToString(format);
                    s = $"{strValue} {unit.Symbol}";
                    return true;
                }
            }
            s = "";
            return false;
        }

        public override bool TryGetValue(TrainCar car, out Quantity<D> v)
        {
            Quantity<D>? result = null;
            try { result = provider(car); }
            catch (Exception exception)
            {
                if (!loggedFailure)
                {
                    Main.mod?.Logger.Warning(Label + " unavailable: " + exception.Message);
                    loggedFailure = true;
                }
            }
            if (result != null && !double.IsNaN(result.Scalar) && !double.IsInfinity(result.Scalar))
            {
                v = result;
                return true;
            }
#pragma warning disable CS8625
            v = default;
#pragma warning restore CS8625
            return false;
        }
    }
}
