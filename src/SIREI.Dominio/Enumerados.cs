namespace SIREI.Dominio;

public enum EstadoIncidencia
{
    Pendiente,
    EnCurso,
    Finalizada
}

public enum Prioridad
{
    Baja,
    Normal,
    Alta,
    Critica
}

/// <summary>Opciones del filtro de estado del listado.</summary>
public enum FiltroEstado
{
    Abiertas,
    Pendiente,
    EnCurso,
    Finalizada,
    Todas
}

public static class EnumeradosTexto
{
    public static string Texto(this EstadoIncidencia estado) => estado switch
    {
        EstadoIncidencia.Pendiente => "Pendiente",
        EstadoIncidencia.EnCurso => "En curso",
        EstadoIncidencia.Finalizada => "Finalizada",
        _ => estado.ToString()
    };

    public static string Texto(this Prioridad prioridad) => prioridad switch
    {
        Prioridad.Baja => "Baja",
        Prioridad.Normal => "Normal",
        Prioridad.Alta => "Alta",
        Prioridad.Critica => "Crítica",
        _ => prioridad.ToString()
    };
}
