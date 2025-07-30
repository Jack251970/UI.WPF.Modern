using System;
using System.Windows;

namespace iNKORE.UI.WPF.Modern.Controls.Primitives
{
    public class FlyoutShowOptions : IEquatable<FlyoutShowOptions>
    {
        public FlyoutPlacementMode Placement { get; set; } = FlyoutPlacementMode.Top;

        public Point? Position { get; set; } = null;

        public FlyoutShowMode ShowMode { get; set; } = FlyoutShowMode.Auto;

        public FlyoutShowOptions()
        {

        }

        public static bool operator ==(FlyoutShowOptions x, FlyoutShowOptions y)
        {
            return x?.Placement == y?.Placement &&
                   x?.Position == y?.Position &&
                   x?.ShowMode == y?.ShowMode;
        }

        public static bool operator !=(FlyoutShowOptions x, FlyoutShowOptions y)
        {
            return !(x == y);
        }

        public bool Equals(FlyoutShowOptions other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            if (obj is FlyoutShowOptions flyoutShowOptions)
            {
                return this == flyoutShowOptions;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Placement.GetHashCode() ^
                   (Position?.GetHashCode() ?? 0) ^
                   ShowMode.GetHashCode();
        }
    }
}
