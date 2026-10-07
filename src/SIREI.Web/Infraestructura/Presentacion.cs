using System.Globalization;
using SIREI.Dominio;

namespace SIREI.Web.Infraestructura;

/// <summary>Textos y clases CSS compartidos por las pantallas.</summary>
public static class Presentacion
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    public static string ClaseEstado(EstadoIncidencia estado) => estado switch
    {
        EstadoIncidencia.Pendiente => "b-info",
        EstadoIncidencia.EnCurso => "b-warning",
        _ => "b-success"
    };

    public static string ClasePrioridad(Prioridad prioridad) => prioridad switch
    {
        Prioridad.Critica => "b-danger",
        Prioridad.Alta => "b-alert",
        _ => "b-neutral"
    };

    /// <summary>03/06/2026 13:12</summary>
    public static string Fecha(DateTimeOffset fecha) => fecha.ToString("dd/MM/yyyy HH:mm", Es);

    /// <summary>el 03/06/2026 a las 13:12</summary>
    public static string FechaFrase(DateTimeOffset fecha) => fecha.ToString("'el 'dd/MM/yyyy' a las 'HH:mm", Es);

    /// <summary>"hoy a las 10:20" o "el 03/06/2026 a las 13:12".</summary>
    public static string Cuando(DateTimeOffset fecha, DateTimeOffset ahora) =>
        fecha.Date == ahora.Date ? fecha.ToString("'hoy a las 'HH:mm", Es) : FechaFrase(fecha);

    /// <summary>"Hoy · 10:20" o "03/06/2026 · 13:12", para el historial.</summary>
    public static string CuandoHistorial(DateTimeOffset fecha, DateTimeOffset ahora) =>
        fecha.Date == ahora.Date ? fecha.ToString("'Hoy · 'HH:mm", Es) : fecha.ToString("dd/MM/yyyy' · 'HH:mm", Es);

    /// <summary>"10:20" o "03/06/2026 10:20", para los mensajes.</summary>
    public static string Hora(DateTimeOffset fecha, DateTimeOffset ahora) =>
        fecha.Date == ahora.Date ? fecha.ToString("HH:mm", Es) : Fecha(fecha);

    public static string Hora(DateTimeOffset fecha) => fecha.ToString("HH:mm", Es);

    /// <summary>Mismo formato que en el navegador: "1,5 MB" o "320 KB".</summary>
    public static string Tamano(long bytes) => bytes >= 1048576
        ? (bytes / 1048576d).ToString("0.0", Es) + " MB"
        : Math.Max(1, (long)Math.Round(bytes / 1024d)) + " KB";

    public static string Plural(int n, string singular, string plural) => n == 1 ? $"1 {singular}" : $"{n} {plural}";
}
