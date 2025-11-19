using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Shiron.Manila.API.Attributes;
using Shiron.Manila.Exceptions;

namespace Shiron.Manila.API;

/// <summary>Base build configuration object for artifacts.</summary>
public abstract class BuildConfig {
    /// <summary>Concatenate properties marked with <see cref="ArtifactKey"/>.</summary>
    /// <returns>Composite key string.</returns>
    /// <exception cref="ConfigurationException">Thrown if key extraction fails.</exception>
    public string GetArtifactKey() {
        try {
            return string.Join(
                "-",
                GetType().GetProperties()
                    .Where(prop => prop.IsDefined(typeof(ArtifactKey), false))
                    .Select(v => v.GetValue(this))
            );
        } catch (Exception e) {
            throw new ConfigurationException("Failed to compute artifact key.", e);
        }
    }
}
