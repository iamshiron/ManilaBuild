
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Shiron.Manila.CLI;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands.API;

public static class APICommandHelpers {
    public static JsonSerializerSettings GetJsonSettings(APICommandSettings settings) {
        return new JsonSerializerSettings {
            Formatting = settings.NoIndent ? Formatting.None : Formatting.Indented,
            NullValueHandling = settings.NoNullValues ? NullValueHandling.Ignore : NullValueHandling.Include,
            DefaultValueHandling = settings.IncludeDefaultValues ? DefaultValueHandling.Include : DefaultValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
    }

    public static string FormatData(object data, APICommandSettings settings) {
        var jsonSettings = GetJsonSettings(settings);
        return JsonConvert.SerializeObject(data, jsonSettings);
    }
}

public class APICommandSettings : DefaultCommandSettings {
    [Description("Include detailed information")]
    [CommandOption("--detailed")] // Can't use constant in attribute
    [DefaultValue(false)]
    public bool Detailed { get; set; }

    [Description("Output in compact format")]
    [CommandOption("--no-indent")]
    [DefaultValue(false)]
    public bool NoIndent { get; set; } = false;

    [Description("No null values in output")]
    [CommandOption("--no-null-values")]
    [DefaultValue(false)]
    public bool NoNullValues { get; set; } = false;

    [Description("Include default values in output")]
    [CommandOption("--include-default-values")]
    [DefaultValue(false)]
    public bool IncludeDefaultValues { get; set; } = false;
}
