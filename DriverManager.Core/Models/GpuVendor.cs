namespace DriverManager.Core.Models;

public enum GpuVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel
}

public static class GpuVendors
{
    public static GpuVendor Detect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GpuVendor.Unknown;
        }

        if (value.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            return GpuVendor.Nvidia;
        }

        if (value.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            return GpuVendor.Intel;
        }

        if (value.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("ATI", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("AMD", StringComparison.OrdinalIgnoreCase))
        {
            return GpuVendor.Amd;
        }

        return GpuVendor.Unknown;
    }
}
