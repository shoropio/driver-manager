using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using System.Globalization;
using System.Management;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Recopila información del sistema (equipo, BIOS, placa base, sistema operativo,
/// procesador y dispositivos) mediante WMI.
/// </summary>
public sealed class SystemInfoService : ISystemInfoService
{
    private const string NoInfo = "No disponible";

    public Task<SystemInfo> GetSystemInfoAsync()
    {
        return Task.Run(() => Build());
    }

    private static SystemInfo Build()
    {
        var info = new SystemInfo { CapturedAt = DateTime.Now };

        var computer = FirstRow("SELECT Manufacturer, Model, TotalPhysicalMemory, SystemType FROM Win32_ComputerSystem",
            "Manufacturer", "Model", "TotalPhysicalMemory", "SystemType");
        var bios = FirstRow("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate, SerialNumber FROM Win32_BIOS",
            "Manufacturer", "SMBIOSBIOSVersion", "ReleaseDate", "SerialNumber");
        var board = FirstRow("SELECT Manufacturer, Product, Version FROM Win32_BaseBoard",
            "Manufacturer", "Product", "Version");
        var os = FirstRow("SELECT Caption, Version, BuildNumber, OSArchitecture FROM Win32_OperatingSystem",
            "Caption", "Version", "BuildNumber", "OSArchitecture");
        var cpu = FirstRow("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor",
            "Name", "NumberOfCores", "NumberOfLogicalProcessors", "MaxClockSpeed");

        info.Sections.Add(new SystemInfoSection
        {
            Title = "Equipo",
            Items = new List<SystemInfoItem>
            {
                new() { Label = "Fabricante", Value = OrNoInfo(computer, "Manufacturer") },
                new() { Label = "Modelo", Value = OrNoInfo(computer, "Model") },
                new() { Label = "Service Tag", Value = OrNoInfo(bios, "SerialNumber") },
                new() { Label = "Tipo de equipo", Value = OrNoInfo(computer, "SystemType") }
            }
        });

        info.Sections.Add(new SystemInfoSection
        {
            Title = "BIOS",
            Items = new List<SystemInfoItem>
            {
                new() { Label = "Fabricante", Value = OrNoInfo(bios, "Manufacturer") },
                new() { Label = "Versión", Value = OrNoInfo(bios, "SMBIOSBIOSVersion") },
                new() { Label = "Fecha", Value = FormatBiosDate(Get(bios, "ReleaseDate")) }
            }
        });

        info.Sections.Add(new SystemInfoSection
        {
            Title = "Placa base",
            Items = new List<SystemInfoItem>
            {
                new() { Label = "Fabricante", Value = OrNoInfo(board, "Manufacturer") },
                new() { Label = "Modelo", Value = OrNoInfo(board, "Product") },
                new() { Label = "Versión", Value = OrNoInfo(board, "Version") }
            }
        });

        info.Sections.Add(new SystemInfoSection
        {
            Title = "Sistema operativo",
            Items = new List<SystemInfoItem>
            {
                new() { Label = "Edición", Value = OrNoInfo(os, "Caption") },
                new() { Label = "Versión (Compilación)", Value = FormatOsVersion(os) },
                new() { Label = "Arquitectura", Value = OrNoInfo(os, "OSArchitecture") }
            }
        });

        info.Sections.Add(new SystemInfoSection
        {
            Title = "Procesador",
            Items = new List<SystemInfoItem>
            {
                new() { Label = "Nombre", Value = OrNoInfo(cpu, "Name") },
                new() { Label = "Núcleos", Value = OrNoInfo(cpu, "NumberOfCores") },
                new() { Label = "Hilos", Value = OrNoInfo(cpu, "NumberOfLogicalProcessors") },
                new() { Label = "Velocidad máxima", Value = FormatClock(Get(cpu, "MaxClockSpeed")) }
            }
        });

        var devices = new SystemInfoSection { Title = "Dispositivos" };
        devices.Items.Add(new SystemInfoItem
        {
            Label = "Gráficos",
            Value = JoinValues(ReadAll("SELECT Name FROM Win32_VideoController", "Name"))
        });
        devices.Items.Add(new SystemInfoItem
        {
            Label = "Sonido",
            Value = JoinValues(ReadAll("SELECT Name FROM Win32_SoundDevice", "Name"))
        });
        devices.Items.Add(new SystemInfoItem
        {
            Label = "Redes e I/O",
            Value = JoinValues(ReadAll("SELECT Name FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE", "Name"))
        });
        devices.Items.Add(new SystemInfoItem
        {
            Label = "Memoria",
            Value = FormatMemory(Get(computer, "TotalPhysicalMemory"), ReadMemoryModules())
        });
        devices.Items.Add(new SystemInfoItem
        {
            Label = "Almacenamiento",
            Value = JoinValues(ReadAll("SELECT Model, Size FROM Win32_DiskDrive", "Model"))
        });
        info.Sections.Add(devices);

        return info;
    }

    private static Dictionary<string, string> FirstRow(string query, params string[] properties)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject item in searcher.Get())
            {
                using (item)
                {
                    foreach (var property in properties)
                    {
                        var value = item[property];
                        if (value is not null)
                        {
                            result[property] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                        }
                    }
                }

                break;
            }
        }
        catch
        {
            // WMI no disponible: se devuelve lo que se haya podido leer.
        }

        return result;
    }

    private static List<string> ReadAll(string query, string property)
    {
        var values = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject item in searcher.Get())
            {
                using (item)
                {
                    var value = item[property];
                    if (value is not null)
                    {
                        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            values.Add(text);
                        }
                    }
                }
            }
        }
        catch
        {
            // WMI no disponible.
        }

        return values;
    }

    private static List<string> ReadMemoryModules()
    {
        var modules = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Capacity, Speed, Manufacturer FROM Win32_PhysicalMemory");
            foreach (ManagementObject item in searcher.Get())
            {
                using (item)
                {
                    var capacity = item["Capacity"] is null ? 0L : Convert.ToInt64(item["Capacity"], CultureInfo.InvariantCulture);
                    var speed = item["Speed"] is null ? 0 : Convert.ToInt32(item["Speed"], CultureInfo.InvariantCulture);
                    var manufacturer = Convert.ToString(item["Manufacturer"], CultureInfo.InvariantCulture) ?? string.Empty;

                    var line = $"{FormatBytes(capacity)} ({speed} MHz)";
                    if (!string.IsNullOrWhiteSpace(manufacturer) &&
                        !manufacturer.Contains("Unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        line = $"{manufacturer} {line}";
                    }

                    modules.Add(line);
                }
            }
        }
        catch
        {
            // WMI no disponible.
        }

        return modules;
    }

    private static string Get(Dictionary<string, string> row, string property)
    {
        return row.TryGetValue(property, out var value) ? value : string.Empty;
    }

    private static string OrNoInfo(Dictionary<string, string> row, string property)
    {
        var value = Get(row, property);
        return string.IsNullOrWhiteSpace(value) ? NoInfo : value.Trim();
    }

    private static string FormatBiosDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 8)
        {
            return NoInfo;
        }

        if (DateTime.TryParseExact(value.Substring(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.ToString("dd/MM/yyyy");
        }

        return value;
    }

    private static string FormatOsVersion(Dictionary<string, string> os)
    {
        var version = Get(os, "Version");
        var build = Get(os, "BuildNumber");
        if (string.IsNullOrWhiteSpace(version) && string.IsNullOrWhiteSpace(build))
        {
            return NoInfo;
        }

        return $"{version.Trim()} (Compilación {build.Trim()})";
    }

    private static string FormatClock(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NoInfo;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mhz)
            ? $"{mhz / 1000.0:0.##} GHz"
            : value;
    }

    private static string FormatMemory(string totalBytes, List<string> modules)
    {
        var total = long.TryParse(totalBytes, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes)
            ? FormatBytes(bytes)
            : NoInfo;

        if (modules.Count == 0)
        {
            return total;
        }

        return $"{total} — {string.Join(" · ", modules)}";
    }

    private static string JoinValues(List<string> values)
    {
        return values.Count == 0 ? NoInfo : string.Join(Environment.NewLine, values);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return NoInfo;
        }

        double value = bytes;
        var unitIndex = 0;
        string[] units = { "bytes", "KB", "MB", "GB", "TB" };
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }
}
