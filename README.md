# Driver Manager

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-12.0-68217A?logo=csharp&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D6?logo=windows&logoColor=white)
![MVVM](https://img.shields.io/badge/MVVM-Arquitectura-6D28D9)
![xUnit](https://img.shields.io/badge/xUnit-Tests-4E7C9F?logo=xunit&logoColor=white)
![Inno Setup](https://img.shields.io/badge/Inno%20Setup-Instalador-0080AA)
![Platform](https://img.shields.io/badge/Platform-Windows%20x64-lightgrey?logo=windows&logoColor=white)
![Release](https://img.shields.io/github/v/release/shoropio/driver-manager?label=release)
![License](https://img.shields.io/badge/License-MIT-blue)

Aplicacion de escritorio en WPF (.NET 10) para gestionar, actualizar, respaldar y restaurar los controladores de un equipo Windows. Interfaz completamente en espanol con tema oscuro.

## Caracteristicas

- **Escaneo de controladores**: lista todos los controladores firmados instalados (via WMI `Win32_PnPSignedDriver`), con fabricante, categoria, version instalada y estado. Los registros fantasma o corruptos de WMI se filtran automaticamente.
- **Busqueda de actualizaciones**: consulta fuentes de actualizaciones para detectar versiones disponibles (NVIDIA GeForce, Dell, Intel & Killer, Windows Update).
- **Gestor de descargas**: cola de descargas con pausa, reanudacion, cancelacion y reintentos con backoff exponencial. Reanudacion de archivos parciales (.part) al reiniciar la aplicacion. Velocidad, ETA y progreso en tiempo real.
- **Instalacion de actualizaciones**: descarga los controladores pendientes e instala paquetes de Windows Update automaticamente. Crea un punto de restauracion antes de instalar.
- **Respaldos**: crea respaldos ZIP de los controladores instalados con manifest JSON y restaura desde respaldos anteriores. Retencion automatica de respaldos (configurable).
- **Puntos de restauracion**: crea puntos de restauracion del sistema de forma manual o automatica antes de aplicar cambios.
- **Monitoreo de GPU**: deteccion de GPUs via WMI y DXGI con informacion de fabricante, VRAM y LUID. Telemetria en tiempo real: utilizacion, temperatura (NVIDIA NVML, AMD ADL, Intel WMI), y uso de memoria dedicada.
- **Software de compania**: detecta instalacion de GeForce Experience, AMD Adrenalin, Intel Arc Control y herramientas de fabricantes.
- **Informacion del sistema**: hardware, BIOS, placa madre, OS, CPU y dispositivos via WMI.
- **Historial**: registro de las operaciones realizadas en la aplicacion.
- **Configuracion**: parametros de fuentes de actualizacion (NVIDIA, Dell, Intel, Windows Update), carpeta de descargas, carpeta de respaldos y limite de respaldos, con persistencia en archivo.
- **Tema oscuro** con interfaz en espanol, diseno con tarjetas y navegacion lateral.

## Requisitos

- Windows 10 u 11 (la aplicacion requiere permisos de administrador: `requireAdministrator`).
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) para compilar y ejecutar desde codigo fuente.

## Compilar y ejecutar

```powershell
# Compilar la solucion
dotnet build DriverManager.slnx

# Ejecutar (se solicitara elevacion de administrador)
dotnet run --project DriverManager.App
```

O ejecutar directamente el binario:

```powershell
.\DriverManager.App\bin\Debug\net10.0-windows\DriverManager.App.exe
```

> La aplicacion pide elevacion de administrador (UAC) al iniciar porque necesita leer informacion completa de los controladores y ejecutar instalaciones, respaldos y puntos de restauracion.

## Tests

```powershell
dotnet test
```

129+ tests cubriendo: helpers compartidos, estado de descargas, backup/retention, GPU software/telemetria, localizacion y state store.

## Estructura del proyecto

```
DriverManager.slnx
├── DriverManager.Core/              # Modelos, interfaces y helpers compartidos
│   ├── Models/                      # AppSettings, DriverInfo, GpuInfo, DownloadItem, etc.
│   ├── Interfaces/                  # IDriverScanner, IDriverUpdateSource, IGpuTelemetryService, etc.
│   ├── FormatHelper.cs              # Formateo de bytes compartido
│   ├── PlatformHelper.cs            # IsAdministrator, IsPnpSuccess
│   └── StringHelper.cs              # Normalize, DeviceNamesMatch
├── DriverManager.Services/          # Implementaciones de servicios
│   └── Implementations/
│       ├── WmiDriverScanner.cs            # Escaneo de controladores (WMI)
│       ├── NvidiaDriverSource.cs          # Fuente de actualizaciones NVIDIA
│       ├── DellDriverSource.cs            # Fuente de actualizaciones Dell
│       ├── IntelDriverSource.cs           # Fuente de actualizaciones Intel & Killer
│       ├── WindowsUpdateDriverSource.cs   # Fuente de Windows Update (WUA COM)
│       ├── DriverUpdateSourceFactory.cs   # Factory para fuentes de actualizacion
│       ├── DriverUpdaterService.cs        # Instalacion de actualizaciones
│       ├── DriverBackupService.cs         # Respaldos, restauracion y retention
│       ├── DownloadManager.cs             # Gestor de descargas con cola y resume
│       ├── GpuInfoService.cs              # Deteccion de GPUs (WMI + DXGI)
│       ├── GpuTelemetryService.cs         # Telemetria de GPU (utilizacion, temp, VRAM)
│       ├── NvidiaTemperatureProvider.cs   # Temperatura via NVML
│       ├── AmdTemperatureProvider.cs      # Temperatura via ADL
│       ├── IntelTemperatureProvider.cs    # Temperatura via WMI
│       ├── GpuSoftwareCatalog.cs          # Catalogo de software de compania
│       ├── GpuSoftwareDetector.cs         # Deteccion de software instalado
│       ├── IntelPageParser.cs             # Parsing de pagina de descarga Intel
│       ├── IntelProductCatalog.cs         # Catalogo de productos Intel
│       ├── SystemInfoService.cs           # Informacion del sistema
│       ├── ProcessRunner.cs               # Ejecucion de procesos
│       ├── DriverStateStore.cs            # Persistencia de estado
│       ├── SettingsService.cs             # Configuracion persistente
│       └── FileLogger.cs                  # Logging a archivo
├── DriverManager.App/                # Aplicacion WPF
│   ├── MainWindow.xaml               # Ventana principal (navegacion + footer)
│   ├── App.xaml                      # Estilos globales y converters
│   ├── ViewModels/                   # ViewModels (Main, Driver, Gpu, Downloads, etc.)
│   ├── Views/                        # Dashboard, Drivers, Gpu, System, Updates,
│   │                                 # Downloads, Backups, Restore, History, Settings
│   ├── Converters/                   # Converters de valor XAML
│   └── Localization/DriverText.cs   # Traducciones y textos en espanol
└── DriverManager.Tests/              # Tests unitarios (xUnit + Moq)
```

## Notas tecnicas

- **Framework**: .NET 10, WPF, C# (nullable habilitado, `ImplicitUsings`).
- **Interfaz de usuario**: dark theme, navegacion lateral, tarjetas de accion, columnas con truncado (ellipsis) y tooltips.
- **Localizacion**: los estados de controladores, categorias conocidas y nombres de dispositivos se traducen al espanol con valores de respaldo.
- **GPU**: deteccion dual via WMI (`Win32_VideoController`) y DXGI (`IDXGIAdapter`) con asociacion por LUID. Temperatura via NVML (NVIDIA), ADL (AMD) y WMI (Intel) con fallback graceful.
- **Datos**: filtrado de registros WMI sin informacion real (dispositivo, nombre, hardware e INF vacios), que provocan versiones corruptas como `2:10.0,...`.
- **CI/CD**: GitHub Actions para build y tests en `windows-latest`. Empaquetado via Inno Setup con code signing opcional.
- **Uso previsto**: sistema de respaldo completo para gestion de controladores de escritorio. Licencia de uso libre.

© 2026 Shoropio Corporation. Todos los derechos reservados.
