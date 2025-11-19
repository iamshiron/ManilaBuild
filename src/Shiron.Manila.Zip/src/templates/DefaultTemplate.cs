
using Shiron.Manila.API.Ext;
using Spectre.Console;

namespace Shiron.Manila.Zip.Templates;

public static class DefaultTemplate {
    public static ProjectTemplate Create() {
        var zip = ManilaZip.Instance;

        return new ProjectTemplateBuilder("default", "Default Zip Template")
            .WithFile(
                new TemplateFileBuilder("/Manila.js", (args) => {
                    var description = AnsiConsole.Ask<string>("What is the description of the project?") ??
                        "A Default Zip project.";

                    return [
                        "const project = Manila.GetProject();",
                        "const workspace = Manila.GetWorkspace();",
                        "",
                        "project.Version(\"1.0.0\");",
                        $"project.Description(\"{description}\");",
                        "",
                        "project.SourceSets({",
                        "    main: Manila.SourceSet(project.GetPath().Join(\"main\")).Include(\"**/*\")",
                        "});",
                        "",
                        "project.Artifacts({",
                        "    main: Manila.Artifact(\"shiron.manila:zip/zip\", (artifact) => {",
                        "        var config = Manila.GetConfig(artifact);",
                        "",
                        "        artifact.Description(\"Zip Main Artifact\")",
                        "",
                        "        Manila.Job(\"build\")",
                        "            .Description(\"Create the Zip File\")",
                        "            .Execute(async () => {",
                        "                await Manila.Build(project, config, artifact);",
                        "            });",
                        "    })",
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
