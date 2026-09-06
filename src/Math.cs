using Brutal.Numerics;
using KSA;

namespace OhMyStars;

public static class VectorMath {
    private const float FloatEpsilon = 1e-6f;
    private const double DoubleEpsilon = 1e-12;

    public static float Length(float3 v) {
        return MathF.Sqrt(
            v.X * v.X +
            v.Y * v.Y +
            v.Z * v.Z
        );
    }

    public static double Length(double3 v) {
        return System.Math.Sqrt(
            v.X * v.X +
            v.Y * v.Y +
            v.Z * v.Z
        );
    }

    public static float3 Normalize(float3 v) {
        float len = Length(v);

        if(len <= FloatEpsilon) {
            return new float3(0.0f, 0.0f, 0.0f);
        }

        return new float3(
            v.X / len,
            v.Y / len,
            v.Z / len
        );
    }

    public static double3 Normalize(double3 v) {
        double len = Length(v);

        if(len <= DoubleEpsilon) {
            return new double3(0.0, 0.0, 0.0);
        }

        return new double3(
            v.X / len,
            v.Y / len,
            v.Z / len
        );
    }

    public static float3 NormalizeToFloat(double3 v) {
        double len = Length(v);

        if(len <= DoubleEpsilon) {
            return new float3(0.0f, 0.0f, 0.0f);
        }

        return new float3(
            (float)(v.X / len),
            (float)(v.Y / len),
            (float)(v.Z / len)
        );
    }

    public static bool IsZero(float3 v) {
        return Length(v) <= FloatEpsilon;
    }

    public static bool IsZero(double3 v) {
        return Length(v) <= DoubleEpsilon;
    }

    public static float3 Cross(float3 a, float3 b) {
        return new float3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );
    }

    public static double3 Cross(double3 a, double3 b) {
        return new double3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );
    }

    public static double Dot(double3 a, double3 b) {
        return
            a.X * b.X +
            a.Y * b.Y +
            a.Z * b.Z;
    }

    public static double3 RotateFromTo(double3 v, double3 from, double3 to) {
        from = Normalize(from);
        to = Normalize(to);

        double3 axis = Cross(from, to);
        double axisLength = Length(axis);

        double dot = Dot(from, to);

        if(axisLength < 1e-12) {
            return dot > 0.0 ? v : new double3(-v.X, -v.Y, -v.Z);
        }

        axis = axis / axisLength;

        double angle = System.Math.Atan2(axisLength, dot);

        return RotateAroundAxis(v, axis, angle);
    }

    public static double3 RotateAroundAxis(double3 v, double3 axis, double angle) {
        double cos = System.Math.Cos(angle);
        double sin = System.Math.Sin(angle);

        return v * cos
            + Cross(axis, v) * sin
            + axis * Dot(axis, v) * (1.0 - cos);
    }
}

public static class EgoTransform {
    public static bool TryVehicleToEgo(
        Vehicle vehicle,
        Camera camera,
        IParentBody parentBody,
        out double3 ego
    ) {
        ego = new double3(0.0, 0.0, 0.0);
        if(vehicle == null) return false;
        if(camera == null) return false;
        if(parentBody == null) return false;
        double3 vehicleCci = vehicle.GetPositionCci();
        double3 vehicleCce = parentBody.GetCci2Cce() * vehicleCci;
        double3 vehicleEcl = parentBody.GetPositionEcl() + vehicleCce;
        ego = camera.EclToEgo(vehicleEcl);
        return true;
    }
}

public static class StarDirectionConverter {
    public static double3 RaDecToDirection(double raHours, double decDegrees) {
        double ra = raHours * System.Math.PI / 12.0;
        double dec = decDegrees * System.Math.PI / 180.0;

        double cosDec = System.Math.Cos(dec);

        // J2000 equatorial direction
        double x = System.Math.Cos(ra) * cosDec;
        double y = System.Math.Sin(ra) * cosDec;
        double z = System.Math.Sin(dec);

        // Rotate by -obliquity around X, matching the star catalog's equatorial-to-ecliptic conversion
        const double obliquityRadians = -23.439281 * System.Math.PI / 180.0;
        double cosObliquity = System.Math.Cos(obliquityRadians);
        double sinObliquity = System.Math.Sin(obliquityRadians);
        double yRotated = y * cosObliquity - z * sinObliquity;
        double zRotated = y * sinObliquity + z * cosObliquity;

        return new double3(x, yRotated, zRotated);
    }

    private static readonly double3 XAxis = new double3(1.0, 0.0, 0.0);
    private static readonly double3 YAxis = new double3(0.0, 1.0, 0.0);
    private static readonly double3 ZAxis = new double3(0.0, 0.0, 1.0);

    // User-adjustable fine-alignment rotation (degrees) applied on top of the ecliptic conversion above,
    // to compensate for any residual offset between this mod's lines and the game's own star field.
    public static double3 ApplyFineAlignment(double3 v, double xDegrees, double yDegrees, double zDegrees) {
        if(xDegrees != 0.0) {
            v = VectorMath.RotateAroundAxis(v, XAxis, xDegrees * System.Math.PI / 180.0);
        }

        if(yDegrees != 0.0) {
            v = VectorMath.RotateAroundAxis(v, YAxis, yDegrees * System.Math.PI / 180.0);
        }

        if(zDegrees != 0.0) {
            v = VectorMath.RotateAroundAxis(v, ZAxis, zDegrees * System.Math.PI / 180.0);
        }

        return v;
    }
}