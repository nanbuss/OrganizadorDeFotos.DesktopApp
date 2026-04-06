# OrganizadorDeFotos.DesktopApp

Aplicación de escritorio WPF para organizar fotos y detectar duplicados en carpetas locales.

## Funcionalidades

- Escaneo de imágenes en una carpeta.
- Detección de grupos de imágenes similares/duplicadas.
- Previsualización de los grupos de duplicados.
- Movimiento de imágenes seleccionadas a una carpeta auxiliar.

## Requisitos

- .NET 10 SDK
- Windows con soporte para WPF

## Cómo construir

Desde la raíz del proyecto:

```bash
dotnet build
```

## Cómo ejecutar

```bash
dotnet run --project OrganizadorDeFotos.DesktopApp.csproj
```

## Estructura principal

- `App.xaml` / `App.xaml.cs` - Entrada de la aplicación WPF.
- `MainWindow.xaml` / `MainWindow.xaml.cs` - Ventana principal.
- `Views/Duplicates` - Vista para mostrar y procesar duplicados.
- `Modules` - Lógica de comparador, manejo de archivos y modelos de datos.

## Nota

Este proyecto utiliza `ImageSharp` para obtener dimensiones de imagen y una biblioteca propia para calcular hashes perceptuales.
