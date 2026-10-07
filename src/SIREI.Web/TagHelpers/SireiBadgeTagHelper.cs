using Microsoft.AspNetCore.Razor.TagHelpers;
using SIREI.Dominio;
using SIREI.Web.Infraestructura;

namespace SIREI.Web.TagHelpers;

/// <summary>
/// Badge de estado o prioridad: <c>&lt;sirei-badge estado="EnCurso" /&gt;</c> o <c>&lt;sirei-badge prioridad="Alta" /&gt;</c>.
/// Pone la clase de color y siempre el texto, para no transmitir información solo con color.
/// </summary>
[HtmlTargetElement("sirei-badge", TagStructure = TagStructure.WithoutEndTag)]
public sealed class SireiBadgeTagHelper : TagHelper
{
    public EstadoIncidencia? Estado { get; set; }

    public Prioridad? Prioridad { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (clase, texto) = (Estado, Prioridad) switch
        {
            ({ } e, null) => (Presentacion.ClaseEstado(e), e.Texto()),
            (null, { } p) => (Presentacion.ClasePrioridad(p), p.Texto()),
            _ => throw new InvalidOperationException("sirei-badge necesita estado o prioridad, no ambos.")
        };

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", $"badge {clase}");
        output.Content.SetContent(texto);
    }
}
