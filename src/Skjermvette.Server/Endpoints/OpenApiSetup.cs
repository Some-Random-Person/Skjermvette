using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Skjermvette.Server.Auth;

namespace Skjermvette.Server.Endpoints;

/// <summary>Configures the OpenAPI document shown in the Swagger UI at /docs (development only).</summary>
public static partial class OpenApiSetup
{
    private const string DeviceScheme = DeviceTokenHandler.SchemeName;

    public static void Configure(OpenApiOptions options)
    {
        AddDeviceTokenAuth(options);
        AddEndpointSummaries(options);
    }

    /// <summary>
    /// Lets the Swagger UI's "Authorize" button send a device token to the device endpoints.
    /// Parent endpoints use the sign-in cookie, which the browser sends after POST /api/auth/login.
    /// </summary>
    private static void AddDeviceTokenAuth(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Skjermvette API";
            document.Info.Description =
                "Parent endpoints use the sign-in cookie: call POST /api/auth/register or /api/auth/login here first, and the browser keeps the cookie. " +
                "Device endpoints use a device token: pair with POST /api/device/pair, then paste the token into Authorize.";
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[DeviceScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                Description = "The token a device gets from POST /api/device/pair. Parent endpoints use the sign-in cookie instead: call POST /api/auth/login first.",
            };
            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            var path = context.Description.RelativePath ?? "";
            if (path.StartsWith("api/device/", StringComparison.OrdinalIgnoreCase) && path != "api/device/pair")
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(DeviceScheme, context.Document)] = [],
                });
            }
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Uses each endpoint handler's XML <c>summary</c> as its description. ASP.NET Core's built-in XML comment
    /// generator didn't attach to this project's AddOpenApi call, so the build's XML documentation file is read instead.
    /// </summary>
    private static void AddEndpointSummaries(OpenApiOptions options)
    {
        var summaries = new Lazy<Dictionary<string, string>>(LoadSummaries);
        options.AddOperationTransformer((operation, context, _) =>
        {
            var method = context.Description.ActionDescriptor.EndpointMetadata.OfType<MethodInfo>().FirstOrDefault();
            if (method?.DeclaringType is { } type && summaries.Value.TryGetValue($"M:{type.FullName}.{method.Name}", out var summary))
                operation.Summary = summary;
            return Task.CompletedTask;
        });
    }

    /// <summary>Method summaries from the XML documentation file, keyed by "M:Namespace.Type.Method" (without parameters).</summary>
    private static Dictionary<string, string> LoadSummaries()
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"{typeof(OpenApiSetup).Assembly.GetName().Name}.xml");
        if (!File.Exists(path))
            return [];

        var summaries = new Dictionary<string, string>();
        foreach (var member in XDocument.Load(path).Descendants("member"))
        {
            var name = (string?)member.Attribute("name");
            if (name is null || !name.StartsWith("M:") || member.Element("summary") is not { } summary)
                continue;
            summaries[name.Split('(')[0]] = Whitespace().Replace(PlainText(summary), " ").Trim();
        }
        return summaries;
    }

    /// <summary>Text of a doc element, with <c>&lt;see cref="..."/&gt;</c> shown as the referenced name.</summary>
    private static string PlainText(XElement element) => string.Concat(element.Nodes().Select(node => node switch
    {
        XText text => text.Value,
        XElement { Name.LocalName: "see" } see => ((string?)see.Attribute("cref") ?? (string?)see.Attribute("langword") ?? "").Split('.', ':')[^1],
        XElement child => PlainText(child),
        _ => "",
    }));

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
