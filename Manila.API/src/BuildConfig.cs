using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Shiron.Manila.API.Attributes;
using Shiron.Manila.Exceptions;

namespace Shiron.Manila.API;

public abstract class BuildConfig {
    public string GetArtifactKey() {
        return string.Join(
            "-",
            GetType().GetProperties()
                .Where(prop => prop.IsDefined(typeof(ArtifactKey), false))
                .Select(v => v.GetValue(this))
        );
    }
}
