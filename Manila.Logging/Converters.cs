using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Shiron.Manila.Logging;

/// <summary>
/// Custom JSON converter for <see cref="Exception"/> types. Serializes key properties
/// of an exception, including its inner exception. Deserialization is not supported.
/// </summary>
public class ExceptionConverter : JsonConverter {
    public override bool CanConvert(Type objectType) {
        return typeof(Exception).IsAssignableFrom(objectType);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        if (value is not Exception exception) {
            writer.WriteNull();
            return;
        }

        var jo = new JObject {
            { "message", exception.Message },
            { "stackTrace", exception.StackTrace },
            { "hResult", exception.HResult },
            { "source", exception.Source }
        };

        if (exception.InnerException != null) {
            jo.Add("innerException", JToken.FromObject(exception.InnerException, serializer));
        }

        jo.WriteTo(writer);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
        throw new Exception("Deserializing exceptions is not supported by this converter.");
    }
}

/// <summary>
/// Custom JSON converter for <see cref="ILogEntry"/>. Serializes log entries into a
/// structured format with a 'type' discriminator and a nested 'data' object for
/// all custom properties. Deserialization is not supported.
/// </summary>
public class LogEntryConverter : JsonConverter<ILogEntry> {
    public override bool CanWrite => true;
    public override bool CanRead => false;

    public override ILogEntry ReadJson(JsonReader reader, Type objectType, ILogEntry? existingValue, bool hasExistingValue, JsonSerializer serializer) {
        throw new Exception("Deserialization of ILogEntry is not supported by this converter.");
    }

    public override void WriteJson(JsonWriter writer, ILogEntry? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        // This nested serializer is configured to handle the 'data' object's contents.
        var nestedSerializer = new JsonSerializer {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter() },
            TypeNameHandling = TypeNameHandling.None,
            // Inherit settings from the parent serializer to respect context.
            ReferenceLoopHandling = serializer.ReferenceLoopHandling,
            PreserveReferencesHandling = serializer.PreserveReferencesHandling
        };

        // CRITICAL: Remove this converter from the nested serializer to prevent a stack overflow.
        var self = nestedSerializer.Converters.FirstOrDefault(c => c is LogEntryConverter);
        if (self != null) {
            nestedSerializer.Converters.Remove(self);
        }

        var namingStrategy = (nestedSerializer.ContractResolver as DefaultContractResolver)?.NamingStrategy;
        string GetFormattedPropertyName(string name) => namingStrategy?.GetPropertyName(name, false) ?? name;

        writer.WriteStartObject();

        // Write metadata properties to the top level.
        writer.WritePropertyName("type");
        writer.WriteValue(value.GetType().FullName);
        writer.WritePropertyName(GetFormattedPropertyName(nameof(ILogEntry.Timestamp)));
        writer.WriteValue(value.Timestamp);
        writer.WritePropertyName(GetFormattedPropertyName(nameof(ILogEntry.Level)));
        nestedSerializer.Serialize(writer, value.Level);

        // Write all other properties into a nested 'data' object.
        writer.WritePropertyName("data");
        writer.WriteStartObject();

        Type type = value.GetType();
        var ignoredProps = new HashSet<string> { nameof(ILogEntry.Timestamp), nameof(ILogEntry.Level) };

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            if (ignoredProps.Contains(property.Name)) {
                continue;
            }

            object? propertyValue = property.GetValue(value);
            writer.WritePropertyName(GetFormattedPropertyName(property.Name));
            nestedSerializer.Serialize(writer, propertyValue);
        }

        writer.WriteEndObject(); // End 'data'
        writer.WriteEndObject(); // End root
    }
}
