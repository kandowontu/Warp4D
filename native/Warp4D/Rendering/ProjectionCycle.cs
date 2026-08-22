namespace Warp4D.Rendering;

internal readonly record struct ProjectionCycleValues(
    int WExtent,
    int CameraProximity,
    int CrossSections,
    int XyRotation,
    int XwRotation,
    int YwRotation,
    int ZwRotation,
    int XzRotation,
    int YzRotation);

internal static class ProjectionCycle
{
    public static ProjectionCycleValues Sample(double seconds)
    {
        // Incommensurate periods prevent the controls from repeatedly lining up in the same pose.
        int wExtent = Oscillate(seconds, minimum: 20, maximum: 100, periodSeconds: 8.5, phase: 0.15);
        int camera = Oscillate(seconds, minimum: 4, maximum: 100, periodSeconds: 11.0, phase: 1.35);
        int crossSections = Oscillate(seconds, minimum: 2, maximum: 6, periodSeconds: 17.0, phase: 2.1);
        int xyRotation = Oscillate(seconds, minimum: -45, maximum: 45, periodSeconds: 15.5, phase: 0.4);
        int xwRotation = Oscillate(seconds, minimum: -85, maximum: 85, periodSeconds: 10.5, phase: 1.1);
        int ywRotation = Oscillate(seconds, minimum: -75, maximum: 75, periodSeconds: 13.0, phase: 2.5);
        int zwRotation = (int)Math.Round(((seconds * 42.0 + 213.0) % 360.0) - 180.0);
        int xzRotation = Oscillate(seconds, minimum: -60, maximum: 60, periodSeconds: 19.0, phase: 1.8);
        int yzRotation = Oscillate(seconds, minimum: -60, maximum: 60, periodSeconds: 23.0, phase: 2.9);
        return new ProjectionCycleValues(
            wExtent,
            camera,
            crossSections,
            xyRotation,
            xwRotation,
            ywRotation,
            zwRotation,
            xzRotation,
            yzRotation);
    }

    public static void Validate()
    {
        ProjectionCycleValues first = Sample(0);
        ProjectionCycleValues later = Sample(4.25);
        if (first == later)
        {
            throw new InvalidOperationException("Automatic projection cycling is not changing values.");
        }

        for (int second = 0; second <= 120; second++)
        {
            ProjectionCycleValues values = Sample(second);
            if (values.WExtent is < 20 or > 100 ||
                values.CameraProximity is < 4 or > 100 ||
                values.CrossSections is < 2 or > 6 ||
                values.XyRotation is < -180 or > 180 ||
                values.XwRotation is < -180 or > 180 ||
                values.YwRotation is < -180 or > 180 ||
                values.ZwRotation is < -180 or > 180 ||
                values.XzRotation is < -180 or > 180 ||
                values.YzRotation is < -180 or > 180)
            {
                throw new InvalidOperationException("Automatic projection cycling exceeded a slider range.");
            }
        }
    }

    private static int Oscillate(
        double seconds,
        int minimum,
        int maximum,
        double periodSeconds,
        double phase)
    {
        double normalized = (Math.Sin(seconds * Math.Tau / periodSeconds + phase) + 1.0) / 2.0;
        return (int)Math.Round(minimum + normalized * (maximum - minimum));
    }
}
