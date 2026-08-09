# Driver Manager

Aplicación de escritorio en WPF (.NET 10) para gestionar, actualizar, respaldar y restaurar los controladores de un equipo Windows. Interfaz completamente en español con tema oscuro.

## Caracteristicas

- **Escaneo de controladores**: lista todos los controladores firmados instalados (via WMI `Win32_PnPSignedDriver`), con fabricante, categoria, version instalada y estado. Los registros fantasma o corruptos de WMI se filtran automaticamente.
- **Busqueda de actualizaciones**: consulta fuentes de actualizaciones para detectar versiones disponibles.
- **Instalacion de actualizaciones**: descarga e instala los controladores pendientes.
- **Respaldos**: crea respaldos ZIP de los controladores instalados y restaura los anteriores.
- **Puntos de restauracion**: crea puntos de restauracion del sistema antes de aplicar cambios.
- **Historial**: registro de las operaciones realizadas en la aplicacion.
- **Configuracion**: parametros de fuentes de actualizacion (NVIDIA, Dell, Windows Update) con persistencia en archivo.
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

## Estructura del proyecto

```
DriverManager.slnx
├── DriverManager.Core/          # Modelos e interfaces (dominio)
├── DriverManager.Services/      # Implementaciones de servicios
│   └── Implementations/
│       ├── WmiDriverScanner.cs        # Escaneo de controladores (WMI)
│       ├── NvidiaDriverSource.cs      # Fuente de actualizaciones NVIDIA
│       ├── DellDriverSource.cs        # Fuente de actualizaciones Dell
│       ├── WindowsUpdateDriverSource.cs # Fuente de Windows Update
│       ├── DriverUpdaterService.cs    # Instalacion de actualizaciones
│       ├── DriverBackupService.cs     # Respaldos y restauracion
│       ├── SettingsService.cs         # Configuracion persistente
│       └── ...                        # Soporte (logger, proceso, factory)
└── DriverManager.App/            # Aplicacion WPF
    ├── MainWindow.xaml            # Ventana principal (navegacion + footer)
    ├── App.xaml                   # Estilos globales y converters
    ├── ViewModels/                # ViewModels (Main, Driver)
    ├── Views/                     # Vistas (Dashboard, Drivers, Updates,
    │                              #  Downloads, Backups, Restore, History, Settings)
    └── Localization/DriverText.cs # Traducciones y textos de respaldo
```

## Notas tecnicas

- **Framework**: .NET 10, WPF, C# (nullable habilitado, `ImplicitUsings`).
- **Interfaz de usuario**: dark theme, navegacion lateral, tarjetas de accion, columnas con truncado (ellipsis) y tooltips.
- **Localizacion**: los estados de controladores, categorias conocidas y nombres de dispositivos se traducen al espanol con valores de respaldo ("No disponible", "Desconocido", etc.).
- **Datos**: filtrado de registros WMI sin informacion real (dispositivo, nombre, hardware e INF vacios), que provocan versiones corruptas como `2:10.0,...`.
- **Uso previsto**: sistema de respaldo completo para gestion de controladores de escritorio. Licencia de uso libre.

© 2026 Shoropio Corporation. Todos los derechos reservados.
