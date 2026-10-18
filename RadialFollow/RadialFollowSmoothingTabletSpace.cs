using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using OpenTabletDriver;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

namespace RadialFollow
{
    [PluginName("AbstractQbit's Radial Follow Smoothing w/Mixmg (Tablet coordinates)")]
    public class RadialFollowSmoothingTabletSpace : IPositionedPipelineElement<IDeviceReport>
    {
        public RadialFollowSmoothingTabletSpace() : base() { }
        
        public PipelinePosition Position => PipelinePosition.PreTransform;
        
        [Property("Smaller Cursor Lag, Less Precision"), DefaultPropertyValue(false)]
        public bool LowLatency
        {
            get => radialCore.LowLatency;
            set
            {
                radialCore.LowLatency = value;
            }
        }


        private double Log1p(double x)
        {
            if (double.IsNaN(x) || double.IsPositiveInfinity(x)) return x;
            if (x < -1.0) return double.NaN;
            if (x == -1.0) return double.NegativeInfinity;

            double u = 1.0 + x;
            if (u == 1.0) return x;
            return Math.Log(u) * x / (u - 1.0);
        }
        
        private double Lerp(double a, double b, double t) {
            return t * a + (1 - t) * b;
        }
        
        // https://mixmg.github.io/smoothing/smoothing-calculator.html
        private void UpdateCalculations(double width, double height)
        {
            const double K_OUTER = 0.0298;
            const double K_INNER = 0.0065;
            const double K_KNEE = 0.5;

            double LEAK_BASE = 0.3;
            if (radialCore.LowLatency)
            {
                LEAK_BASE = 0.17;
            }

            double outerRaw = K_OUTER * width;
            double innerRaw = K_INNER * height;
            
            // const double EPSILON = 1e-6;
            // double unused = Log1p(Math.Pow(outerRaw / (innerRaw + EPSILON), 0.6));

            double kneeInput = Math.Tanh(K_KNEE * (outerRaw - innerRaw));
            double softKneeScale = Log1p(Math.Exp(-kneeInput));
            double outerRadius = Lerp(outerRaw, 0, 0.3);
            double aspectRatio = width / height;
            double leakCoeff = LEAK_BASE * Log1p(aspectRatio);
            
            radialCore.OuterRadius = outerRadius;
            radialCore.InnerRadius = innerRaw;
            radialCore.SoftKneeScale = softKneeScale;
            radialCore.SmoothingCoefficient = innerRaw;
            radialCore.SmoothingLeakCoefficient = leakCoeff;
            
            Log.Write("RadialFollow+Mixmg", "Tablet Dimensions: " + width + "x" + height + " (" + aspectRatio + ":1)", LogLevel.Info);
            Log.Write("RadialFollow+Mixmg", "Outer: " + radialCore.OuterRadius + "mm, Inner: " + radialCore.InnerRadius + "mm, Initial: " + radialCore.SmoothingCoefficient + ", Soft Knee: " + radialCore.SoftKneeScale + ", SLC: " + radialCore.SmoothingLeakCoefficient, LogLevel.Info);
        }

        public event Action<IDeviceReport> Emit;

        public void Consume(IDeviceReport value)
        {
            if (Driver is Driver driver)
            {
                var tree = driver.InputDevices.FirstOrDefault(t => t.Properties.Name == TabletReference.Properties.Name);
                if (tree?.OutputMode is AbsoluteOutputMode abs)
                {
                    if (Math.Abs(tabletSize.X - abs.Input.Width) > 0.01 || Math.Abs(tabletSize.Y - abs.Input.Height) > 0.01)
                    {
                        Log.Write("RadialFollow+Mixmg", "Tablet dimensions updated, recalculating...", LogLevel.Info);
                        UpdateCalculations(abs.Input.Width, abs.Input.Height);
                    }
                    
                    tabletSize = new Vector2(abs.Input.Width, abs.Input.Height);
                }
            }
            
            if (value is ITabletReport report)
            {
                report.Position = radialCore.Filter(report.Position * mmScale) / mmScale;
                value = report;
            }
            
            Emit?.Invoke(value);
        }

        RadialFollowCore radialCore = new RadialFollowCore();

        [Resolved]
        public IDriver Driver { get; set; }
        
        [TabletReference]
        public TabletReference TabletReference
        {
            get => tabletReference;
            set
            {
                tabletReference = value;
                var digitizer = value.Properties.Specifications.Digitizer;
                mmScale = new Vector2
                {
                    X = digitizer.Width / digitizer.MaxX,
                    Y = digitizer.Height / digitizer.MaxY
                };
            }
        }

        private Vector2 mmScale = Vector2.One;
        private Vector2 tabletSize = new(-1, -1);
        private TabletReference tabletReference;
    }
}
