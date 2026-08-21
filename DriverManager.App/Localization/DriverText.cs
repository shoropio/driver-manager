using System.Collections.Generic;
using DriverManager.Core.Models;

namespace DriverManager.App.Localization;

/// <summary>
/// Proporciona textos en español y valores de respaldo para datos de controladores
/// obtenidos del sistema. Nunca se debe mostrar un dato vacío, null o un nombre
/// técnico en inglés como texto visible.
/// </summary>
public static class DriverText
{
    public const string NoInfo = "No disponible";
    public const string Unknown = "Desconocido";

    public static string StatusText(DriverStatus status) => status switch
    {
        DriverStatus.Installed => "Instalado",
        DriverStatus.UpToDate => "Actualizado",
        DriverStatus.UpdateAvailable => "Requiere actualización",
        DriverStatus.ProblemDetected => "Error",
        DriverStatus.BackupAvailable => "Respaldo disponible",
        _ => Unknown
    };

    public static DriverStatus? StatusFromText(string? text) => text switch
    {
        "Instalado" => DriverStatus.Installed,
        "Actualizado" => DriverStatus.UpToDate,
        "Requiere actualización" => DriverStatus.UpdateAvailable,
        "Error" => DriverStatus.ProblemDetected,
        "Respaldo disponible" => DriverStatus.BackupAvailable,
        _ => null
    };

    public static string DeviceNameText(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Dispositivo desconocido";
        }

        var trimmed = name.Trim();

        if (trimmed.StartsWith("System Firmware", System.StringComparison.Ordinal))
        {
            return "Firmware del sistema" + trimmed["System Firmware".Length..];
        }

        if (trimmed.StartsWith("Intel Processor", System.StringComparison.Ordinal))
        {
            return "Procesador Intel" + trimmed["Intel Processor".Length..];
        }

        if (trimmed.StartsWith("Intel", System.StringComparison.OrdinalIgnoreCase) &&
            trimmed.Contains(" Controller", System.StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Replace(" Controller", " Controlador", System.StringComparison.Ordinal);
        }

        return KnownDeviceNames.TryGetValue(trimmed, out var translated) ? translated : trimmed;
    }

    public static string CategoryText(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "Sin categoría";
        }

        return KnownCategories.TryGetValue(category.Trim(), out var translated) ? translated : category.Trim();
    }

    public static string ManufacturerText(string? manufacturer)
    {
        return string.IsNullOrWhiteSpace(manufacturer) ? "Fabricante desconocido" : manufacturer.Trim();
    }

    public static string VersionText(string? version)
    {
        return string.IsNullOrWhiteSpace(version) ? NoInfo : version.Trim();
    }

    public static string GpuVendorText(GpuVendor vendor) => vendor switch
    {
        GpuVendor.Nvidia => "NVIDIA",
        GpuVendor.Amd => "AMD",
        GpuVendor.Intel => "Intel",
        _ => "Desconocido"
    };

    public static string SoftwareStatusText(SoftwareInstallStatus status) => status switch
    {
        SoftwareInstallStatus.Installed => "Instalado",
        SoftwareInstallStatus.NotInstalled => "No instalado",
        _ => "No disponible"
    };

    public static string MaintenanceIssueTypeText(MaintenanceIssueType type) => type switch
    {
        MaintenanceIssueType.Obsolete => "Obsoleto",
        MaintenanceIssueType.Missing => "Faltante",
        MaintenanceIssueType.Broken => "Con error",
        MaintenanceIssueType.Unsigned => "Sin firmar",
        _ => Unknown
    };

    public static string MaintenanceSeverityText(MaintenanceSeverity severity) => severity switch
    {
        MaintenanceSeverity.Critical => "Crítico",
        MaintenanceSeverity.Warning => "Advertencia",
        MaintenanceSeverity.Info => "Informativo",
        _ => Unknown
    };

    public static string FormatSize(long bytes)
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

        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{value:0.##} {units[unitIndex]}");
    }

    public static string FormatPercent(double? percent)
    {
        return percent is null
            ? NoInfo
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{percent:0.#} %");
    }

    public static string FormatTemperature(double? celsius)
    {
        return celsius is null
            ? NoInfo
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{celsius:0} °C");
    }

    public static string FormatMemory(long? usageBytes, long? limitBytes)
    {
        if (usageBytes is null || usageBytes <= 0)
        {
            return NoInfo;
        }

        return limitBytes is not null && limitBytes > 0
            ? $"{FormatSize(usageBytes.Value)} de {FormatSize(limitBytes.Value)}"
            : FormatSize(usageBytes.Value);
    }

    private static readonly Dictionary<string, string> KnownCategories = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["1394"] = "IEEE 1394",
        ["APPLIANCE"] = "Electrodoméstico",
        ["AUDIOENDPOINT"] = "Punto final de audio",
        ["AUDIOPROCESSINGOBJECT"] = "Objeto de procesamiento de audio",
        ["AVC"] = "Dispositivo AVC",
        ["BATTERY"] = "Batería",
        ["BIOMETRIC"] = "Biometría",
        ["BLUETOOTH"] = "Bluetooth",
        ["CAMERA"] = "Cámara",
        ["CDROM"] = "Unidad de CD-ROM",
        ["COMPUTER"] = "Equipo",
        ["DELLINSTRUMENTATION"] = "Instrumentación Dell",
        ["DISKDRIVE"] = "Unidad de disco",
        ["DISPLAY"] = "Pantalla",
        ["EAP"] = "EAP",
        ["FIRMWARE"] = "Firmware",
        ["FLOPPYDISK"] = "Disquetera",
        ["GPS"] = "GPS",
        ["HDC"] = "Controlador de discos duros",
        ["HIDCLASS"] = "Dispositivo HID",
        ["IMAGE"] = "Imagen",
        ["KEYBOARD"] = "Teclado",
        ["MCD"] = "Cambiador de CD",
        ["MEDIA"] = "Multimedia",
        ["MEDIUM_CHANGER"] = "Cambiador de medios",
        ["MODEM"] = "Módem",
        ["MONITOR"] = "Monitor",
        ["MOUSE"] = "Ratón",
        ["MULTIFUNCTION"] = "Multifunción",
        ["MULTIPORT_SERIAL"] = "Puerto serie múltiple",
        ["NET"] = "Red",
        ["NETCLIENT"] = "Cliente de red",
        ["NETSERVICE"] = "Servicio de red",
        ["NETTRANS"] = "Transporte de red",
        ["PCMCIA"] = "PCMCIA",
        ["PCMCIA_MTD"] = "Memoria PCMCIA",
        ["PORTS"] = "Puertos",
        ["PRINTER"] = "Impresora",
        ["PRINTQUEUE"] = "Impresora",
        ["PROCESSOR"] = "Procesador",
        ["RAMDISK"] = "Disco RAM",
        ["SCSIADAPTER"] = "Adaptador SCSI",
        ["SDHOST"] = "Host de tarjeta SD",
        ["SECURITYACCELERATOR"] = "Acelerador de seguridad",
        ["SECURITYDEVICES"] = "Dispositivos de seguridad",
        ["SENSOR"] = "Sensor",
        ["SMARTCARDREADER"] = "Lector de tarjetas inteligentes",
        ["SMART_CARD_READER"] = "Lector de tarjetas inteligentes",
        ["SOFTWARECOMPONENT"] = "Componente de software",
        ["SOFTWAREDEVICE"] = "Dispositivo de software",
        ["SOUND"] = "Sonido",
        ["STORAGECONTROLLER"] = "Controlador de almacenamiento",
        ["SYSTEM"] = "Sistema",
        ["TAPEDRIVE"] = "Unidad de cinta",
        ["UCM"] = "UCM (USB-C)",
        ["USB"] = "USB",
        ["USBDEVICE"] = "Dispositivo USB",
        ["VOLUME"] = "Volumen",
        ["VOLUMESNAPSHOT"] = "Instantánea de volumen",
        ["WCEUSBS"] = "Dispositivo USB de comunicaciones",
        ["WPD"] = "Dispositivos portátiles"
    };

    private static readonly Dictionary<string, string> KnownDeviceNames = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["ACPI Lid"] = "Tapa ACPI",
        ["ACPI Processor Aggregator"] = "Agregador de procesador ACPI",
        ["ACPI Sleep Button"] = "Botón de suspensión ACPI",
        ["ACPI Wake Alarm"] = "Alarma de activación ACPI",
        ["ACPI x64-based PC"] = "PC basado en ACPI x64",
        ["Audio Endpoint"] = "Punto final de audio",
        ["Composite Bus Enumerator"] = "Enumerador de bus compuesto",
        ["Computer Device"] = "Dispositivo de equipo",
        ["Converted Portable Device Control device"] = "Dispositivo de control de dispositivo portátil convertido",
        ["DellInstrumentation Device"] = "Dispositivo de instrumentación Dell",
        ["Detection Verification"] = "Verificación de detección",
        ["Device Firmware"] = "Firmware del dispositivo",
        ["Disk drive"] = "Unidad de disco",
        ["Docking Station"] = "Estación de acoplamiento",
        ["External"] = "Externo",
        ["Generic PnP Monitor"] = "Monitor PnP genérico",
        ["Generic software device"] = "Dispositivo de software genérico",
        ["Generic USB Hub"] = "Concentrador USB genérico",
        ["Generic volume shadow copy"] = "Copia de sombra de volumen genérica",
        ["HID Keyboard Device"] = "Dispositivo de teclado HID",
        ["HID PCI Minidriver for ISS"] = "Minicontrolador PCI HID para ISS",
        ["HID Sensor Collection V2"] = "Colección de sensores HID V2",
        ["HID-compliant consumer control device"] = "Dispositivo de control de consumo compatible con HID",
        ["HID-compliant mouse"] = "Ratón compatible con HID",
        ["HID-compliant system controller"] = "Controlador del sistema compatible con HID",
        ["HID-compliant touch pad"] = "Panel táctil compatible con HID",
        ["HID-compliant vendor-defined device"] = "Dispositivo definido por el fabricante compatible con HID",
        ["HID-compliant wireless radio controls"] = "Controles inalámbricos de radio compatibles con HID",
        ["High Definition Audio Controller"] = "Controlador de audio de alta definición",
        ["High precision event timer"] = "Temporizador de eventos de alta precisión",
        ["Hyper-V Virtual Switch Extension Adapter"] = "Adaptador de extensión de conmutador virtual Hyper-V",
        ["I2C HID Device"] = "Dispositivo HID I2C",
        ["ISS Dynamic Bus Enumerator"] = "Enumerador de bus dinámico ISS",
        ["Local Print Queue"] = "Cola de impresión local",
        ["Microsoft AC Adapter"] = "Adaptador de CA de Microsoft",
        ["Microsoft ACPI-Compliant Control Method Battery"] = "Batería de método de control compatible con ACPI de Microsoft",
        ["Microsoft ACPI-Compliant Embedded Controller"] = "Controlador integrado compatible con ACPI de Microsoft",
        ["Microsoft ACPI-Compliant System"] = "Sistema compatible con ACPI de Microsoft",
        ["Microsoft Basic Display Driver"] = "Controlador de pantalla básico de Microsoft",
        ["Microsoft Basic Render Driver"] = "Controlador de representación básico de Microsoft",
        ["Microsoft Hyper-V NT Kernel Integration VSP"] = "VSP de integración del kernel NT de Hyper-V de Microsoft",
        ["Microsoft Hyper-V PCI Server"] = "Servidor PCI de Hyper-V de Microsoft",
        ["Microsoft Hyper-V Virtual Disk Server"] = "Servidor de disco virtual de Hyper-V de Microsoft",
        ["Microsoft Hyper-V Virtual Machine Bus Provider"] = "Proveedor de bus de máquina virtual de Hyper-V de Microsoft",
        ["Microsoft Hyper-V Virtualization Infrastructure Driver"] = "Controlador de infraestructura de virtualización de Hyper-V de Microsoft",
        ["Microsoft Hypervisor Service"] = "Servicio de hipervisor de Microsoft",
        ["Microsoft Input Configuration Device"] = "Dispositivo de configuración de entrada de Microsoft",
        ["Microsoft Kernel Debug Network Adapter"] = "Adaptador de red de depuración del kernel de Microsoft",
        ["Microsoft Storage Spaces Controller"] = "Controlador de Espacios de almacenamiento de Microsoft",
        ["Microsoft Streaming Service Proxy"] = "Proxy de servicio de streaming de Microsoft",
        ["Microsoft System Management BIOS Driver"] = "Controlador del BIOS de administración del sistema de Microsoft",
        ["Microsoft UEFI-Compliant System"] = "Sistema compatible con UEFI de Microsoft",
        ["Microsoft VHD Loopback Controller"] = "Controlador de bucle invertido de VHD de Microsoft",
        ["Microsoft Virtual Drive Enumerator"] = "Enumerador de unidad virtual de Microsoft",
        ["Microsoft Wi-Fi Direct Virtual Adapter"] = "Adaptador virtual Wi-Fi Direct de Microsoft",
        ["Microsoft Windows Management Interface for ACPI"] = "Interfaz de administración de Windows de Microsoft para ACPI",
        ["Motherboard resources"] = "Recursos de la placa base",
        ["NDIS Virtual Network Adapter Enumerator"] = "Enumerador de adaptador de red virtual NDIS",
        ["Numeric data processor"] = "Procesador de datos numéricos",
        ["PCI Express Downstream Switch Port"] = "Puerto de conmutador de bajada PCI Express",
        ["PCI Express Root Complex"] = "Complejo raíz PCI Express",
        ["PCI Express Upstream Switch Port"] = "Puerto de conmutador de subida PCI Express",
        ["PCI standard host CPU bridge"] = "Puente de CPU de host PCI estándar",
        ["Plug and Play Software Device Enumerator"] = "Enumerador de dispositivos de software Plug and Play",
        ["Portable Device Control device"] = "Dispositivo de control de dispositivo portátil",
        ["Programmable interrupt controller"] = "Controlador de interrupciones programable",
        ["PS/2 Compatible Mouse"] = "Ratón compatible con PS/2",
        ["Remote Desktop Device Redirector Bus"] = "Bus de redirección de dispositivos de escritorio remoto",
        ["Standard PS/2 Keyboard"] = "Teclado PS/2 estándar",
        ["System CMOS/real time clock"] = "Reloj CMOS/en tiempo real del sistema",
        ["System timer"] = "Temporizador del sistema",
        ["Trusted Platform Module 2.0"] = "Módulo de plataforma segura 2.0",
        ["UCM-UCSI ACPI Device"] = "Dispositivo ACPI UCM-UCSI",
        ["UMBus Root Bus Enumerator"] = "Enumerador de bus raíz UMBus",
        ["USB Composite Device"] = "Dispositivo USB compuesto",
        ["USB Input Device"] = "Dispositivo de entrada USB",
        ["USB Mass Storage Device"] = "Dispositivo de almacenamiento masivo USB",
        ["USB Root Hub (USB 3.0)"] = "Concentrador raíz USB (USB 3.0)",
        ["USB Video Device"] = "Dispositivo de vídeo USB",
        ["USB xHCI Compliant Host Controller"] = "Controlador host compatible con USB xHCI",
        ["Volume"] = "Volumen",
        ["Volume Manager"] = "Administrador de volúmenes",
        ["WAN Miniport (IKEv2)"] = "Minipuerto WAN (IKEv2)",
        ["WAN Miniport (IP)"] = "Minipuerto WAN (IP)",
        ["WAN Miniport (IPv6)"] = "Minipuerto WAN (IPv6)",
        ["WAN Miniport (L2TP)"] = "Minipuerto WAN (L2TP)",
        ["WAN Miniport (Network Monitor)"] = "Minipuerto WAN (Monitor de red)",
        ["WAN Miniport (PPPOE)"] = "Minipuerto WAN (PPPoE)",
        ["WAN Miniport (PPTP)"] = "Minipuerto WAN (PPTP)",
        ["WAN Miniport (SSTP)"] = "Minipuerto WAN (SSTP)"
    };
}
