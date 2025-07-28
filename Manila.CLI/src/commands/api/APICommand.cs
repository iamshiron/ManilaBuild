
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Shiron.Manila.CLI;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Commands.API;

public static class APICommandHelpers {
    public static JsonSerializerSettings GetJsonSettings(bool noIndent, bool noNullValues, bool includeDefaultValues) {
        return new JsonSerializerSettings {
            Formatting = noIndent ? Formatting.None : Formatting.Indented,
            NullValueHandling = noNullValues ? NullValueHandling.Ignore : NullValueHandling.Include,
            DefaultValueHandling = includeDefaultValues ? DefaultValueHandling.Include : DefaultValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
    }

    public static string FormatData(object data, bool noIndent, bool noNullValues, bool includeDefaultValues) {
        var jsonSettings = GetJsonSettings(noIndent, noNullValues, includeDefaultValues);
        return JsonConvert.SerializeObject(data, jsonSettings);
    }
}

public class APICommandSettings : DefaultCommandSettings { }
