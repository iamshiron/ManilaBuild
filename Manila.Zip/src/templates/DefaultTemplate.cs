
using Shiron.Manila.API.Ext;
using Spectre.Console;

namespace Shiron.Manila.Zip.Templates;

public static class DefaultTemplate {
    public static ProjectTemplate Create() {
        return new ProjectTemplateBuilder("default", "Default Zip Template")
            .WithFile(
                new TemplateFileBuilder("/Manila.js", (args) => {
                    var description = AnsiConsole.Ask<string>("What is the description of the project?") ??
                        "A Default Zip project.";

                    return [
                        "var project = Manila.getProject();",
                        "var workspace = Manila.getWorkspace();",
                        "",
                        "project.Version(\"1.0.0\");",
                        $"project.Description(\"{description}\");",
                        "",
                        "project.SourceSets(new Dictionary<string, object> {",
                        "    [\"main\"] = Manila.SourceSet(project.GetPath().Join(\"main\")).Include(\"**/*\")",
                        "});",
                        "",
                        "project.Artifacts(new Dictionary<string, object> {",
                        "    [\"main\"] = Manila.Artifact(artifact => {",
                        "        var config = Manila.GetConfig(artifact);",
                        "",
                        "        Manila.Job(\"build\")",
                        "            .Description(\"Create the Zip File\")",
                        "            .Execute(async () => {",
                        "                await Manila.Build(project, config, artifact);",
                        "            });",
                        "    }).description(\"Zip Main Artifact\")",
                        "});",
                        ""
                    ];
                })
            )
            .WithFile(
                new TemplateFileBuilder("/main/test.txt", (args) => {
                    return ["This is a test file for the default zip template."];
                })
            )
        .Build();
    }
}
