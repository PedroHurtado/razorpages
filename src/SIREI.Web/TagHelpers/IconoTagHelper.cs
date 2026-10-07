using Microsoft.AspNetCore.Razor.TagHelpers;

namespace SIREI.Web.TagHelpers;

/// <summary>
/// Icono Lucide en línea y decorativo: <c>&lt;icono nombre="plus" class="i i-sm" /&gt;</c>.
/// Siempre lleva aria-hidden: el texto o el aria-label del control da el nombre accesible.
/// </summary>
[HtmlTargetElement("icono", TagStructure = TagStructure.WithoutEndTag)]
public sealed class IconoTagHelper : TagHelper
{
    private static readonly Dictionary<string, string> Trazos = new()
    {
        ["alert-circle"] = """<circle cx="12" cy="12" r="10"></circle><path d="M12 8v4"></path><path d="M12 16h.01"></path>""",
        ["arrow-left"] = """<path d="m12 19-7-7 7-7"></path><path d="M19 12H5"></path>""",
        ["bold"] = """<path d="M6 12h9a4 4 0 0 1 0 8H7a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1h7a4 4 0 0 1 0 8"></path>""",
        ["check"] = """<path d="M20 6 9 17l-5-5"></path>""",
        ["chevron-left"] = """<path d="m15 18-6-6 6-6"></path>""",
        ["chevron-right"] = """<path d="m9 18 6-6-6-6"></path>""",
        ["eye"] = """<path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"></path><circle cx="12" cy="12" r="3"></circle>""",
        ["file-text"] = """<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><path d="M14 2v6h6"></path><path d="M16 13H8"></path><path d="M16 17H8"></path>""",
        ["home"] = """<path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path><path d="M9 22V12h6v10"></path>""",
        ["info"] = """<circle cx="12" cy="12" r="10"></circle><path d="M12 16v-4"></path><path d="M12 8h.01"></path>""",
        ["italic"] = """<path d="M19 4h-9"></path><path d="M14 20H5"></path><path d="M15 4 9 20"></path>""",
        ["link"] = """<path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"></path><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"></path>""",
        ["list"] = """<path d="M8 6h13"></path><path d="M8 12h13"></path><path d="M8 18h13"></path><path d="M3 6h.01"></path><path d="M3 12h.01"></path><path d="M3 18h.01"></path>""",
        ["lock"] = """<rect x="3" y="11" width="18" height="11" rx="2"></rect><path d="M7 11V7a5 5 0 0 1 10 0v4"></path>""",
        ["message-square"] = """<path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path>""",
        ["paperclip"] = """<path d="m21.44 11.05-9.19 9.19a6 6 0 0 1-8.49-8.49l8.57-8.57A4 4 0 1 1 18 8.84l-8.59 8.57a2 2 0 0 1-2.83-2.83l8.49-8.48"></path>""",
        ["phone"] = """<path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z"></path>""",
        ["plus"] = """<path d="M5 12h14"></path><path d="M12 5v14"></path>""",
        ["refresh-cw"] = """<path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"></path><path d="M3 21v-5h5"></path><path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"></path><path d="M21 3v5h-5"></path>""",
        ["rotate-ccw"] = """<path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><path d="M3 3v5h5"></path>""",
        ["save"] = """<path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path><path d="M17 21v-8H7v8"></path><path d="M7 3v5h8"></path>""",
        ["search"] = """<circle cx="11" cy="11" r="8"></circle><path d="m21 21-4.3-4.3"></path>""",
        ["send"] = """<path d="m22 2-7 20-4-9-9-4Z"></path><path d="M22 2 11 13"></path>""",
        ["star"] = """<path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"></path>""",
        ["upload"] = """<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><path d="m17 8-5-5-5 5"></path><path d="M12 3v12"></path>""",
        ["user-check"] = """<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"></path><circle cx="9" cy="7" r="4"></circle><path d="m16 11 2 2 4-4"></path>""",
        ["x"] = """<path d="M18 6 6 18"></path><path d="m6 6 12 12"></path>"""
    };

    public required string Nombre { get; set; }

    public string Class { get; set; } = "i";

    public string? Style { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!Trazos.TryGetValue(Nombre, out var trazos))
        {
            throw new InvalidOperationException($"Icono desconocido: {Nombre}");
        }

        output.TagName = "svg";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", Class);
        output.Attributes.SetAttribute("viewBox", "0 0 24 24");
        output.Attributes.SetAttribute("aria-hidden", "true");
        if (Style is not null)
        {
            output.Attributes.SetAttribute("style", Style);
        }
        output.Content.SetHtmlContent(trazos);
    }
}
