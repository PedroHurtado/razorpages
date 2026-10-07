using SIREI.Dominio;

namespace SIREI.Servicios.Fake;

/// <summary>Datos de ejemplo tomados de los wireframes y de las capturas.</summary>
internal static class DatosIniciales
{
    public const string SolicitanteCau = "CAU N2 (en nombre del interesado)";

    public static readonly UsuarioActual UsuarioActual =
        new("ctpinedo", "Carlos T. Pinedo", "Pinedo Vilches, Carlos Tomás", "CP", "CAU N2");

    public static readonly IReadOnlyList<Persona> Personas =
    [
        new("ctpinedo", "Pinedo Vilches, Carlos Tomás", "Carlos T. Pinedo", "CP", true, "[EXT]"),
        new("mcorral", "Corral Camarzana, María Yermo", "María Yermo Corral", "MC", false, "[EXT]"),
        new("cbaldo", "Baldó, Carlos", "Carlos Baldó", "CB", false, "[EXT]"),
        new("descudero", "Escudero, D.", "D. Escudero", "DE", true, "[EXT]")
    ];

    public static readonly IReadOnlyList<Gestor> Gestores =
    [
        new("ctpinedo", "Carlos T. Pinedo"),
        new("myermo", "María del Yermo"),
        new("cbaldo", "Carlos Baldó")
    ];

    public static readonly IReadOnlyList<string> Categorias =
    [
        "Permisos y autorizaciones",
        "Aplicaciones corporativas",
        "Correo electrónico",
        "Equipos y periféricos",
        "Red y conectividad",
        "Otros"
    ];

    public static List<IncidenciaEntidad> Incidencias() =>
    [
        Crear(1512, "Escritura en SIREI", "No puedo guardar cambios en una incidencia.", "Aplicaciones corporativas", "mcorral", Fecha(3, 6, 15, 15), EstadoIncidencia.EnCurso, "myermo", Prioridad.Normal),
        Crear(1510, "Buenas noches.", "Mensaje de prueba.", "Otros", "cbaldo", Fecha(3, 6, 13, 55), EstadoIncidencia.EnCurso, "cbaldo", Prioridad.Normal),
        Crear(1507, "Probando", "estoy probando", "Permisos y autorizaciones", "mcorral", Fecha(3, 6, 13, 12), EstadoIncidencia.Pendiente, null, Prioridad.Normal),
        Crear(1505, "Visita de su santidad el Papa", "Preparar los equipos de la sala de prensa para la visita.", "Equipos y periféricos", "mcorral", Fecha(3, 6, 10, 40), EstadoIncidencia.EnCurso, "cbaldo", Prioridad.Alta),
        Crear(1500, "I", "Prueba de título de un solo carácter.", "Otros", "cbaldo", Fecha(29, 5, 14, 10), EstadoIncidencia.EnCurso, "cbaldo", Prioridad.Normal),
        Crear(1499, "ppppppppp", "Prueba de título sin espacios.", "Otros", "cbaldo", Fecha(29, 5, 13, 40), EstadoIncidencia.EnCurso, "cbaldo", Prioridad.Normal),
        Crear(1489, "prueba", "Prueba.", "Otros", "cbaldo", Fecha(22, 5, 14, 20), EstadoIncidencia.EnCurso, "cbaldo", Prioridad.Normal),
        Crear(1487, "Prueba exceder ancho descripción.", "Prueba de una descripción larga para comprobar que el texto se ajusta al ancho disponible sin desbordar la pantalla.", "Aplicaciones corporativas", "cbaldo", Fecha(22, 5, 13, 5), EstadoIncidencia.EnCurso, "cbaldo", Prioridad.Critica),
        Crear(1486, "Prueba", "Prueba de usuario VIP.", "Otros", "descudero", Fecha(22, 5, 12, 38), EstadoIncidencia.EnCurso, "cbaldo", Prioridad.Normal),
        Crear(1485, "Ejemplo ext.tel", "Comprobar la extensión telefónica del interesado.", "Red y conectividad", "ctpinedo", Fecha(22, 5, 10, 38), EstadoIncidencia.EnCurso, "cbaldo", Prioridad.Normal)
    ];

    private static DateTimeOffset Fecha(int dia, int mes, int hora, int minuto) =>
        new(new DateTime(2026, mes, dia, hora, minuto, 0, DateTimeKind.Local));

    private static IncidenciaEntidad Crear(
        int id, string titulo, string descripcion, string categoria, string interesado,
        DateTimeOffset fecha, EstadoIncidencia estado, string? asignado, Prioridad prioridad)
    {
        var incidencia = new IncidenciaEntidad
        {
            Id = id,
            Titulo = titulo,
            Descripcion = descripcion,
            Categoria = categoria,
            Interesado = interesado,
            Solicitante = SolicitanteCau,
            FechaEntrada = fecha,
            FechaActualizacion = fecha,
            Estado = estado,
            Asignado = asignado,
            Prioridad = prioridad
        };
        incidencia.Historial.Add(new EventoHistorial("Creada por CAU N2", fecha));

        if (asignado is not null)
        {
            var nombre = Gestores.First(g => g.Usuario == asignado).Nombre;
            incidencia.FechaActualizacion = fecha.AddMinutes(20);
            incidencia.Historial.Insert(0, new EventoHistorial($"Asignada a {nombre} · En curso", incidencia.FechaActualizacion));
        }

        return incidencia;
    }
}
