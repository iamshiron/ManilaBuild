
using Shiron.Manila.API.Ext;
using Spectre.Console;

namespace Shiron.Manila.Zip.Templates;

public static class DefaultTemplate {
    public static ProjectTemplate Create() {
        return new ProjectTemplateBuilder("default", "Default Zip Template")
            .WithFile(
                new TemplateFileBuilder("/Manila.cs", (args) => {
                    var description = AnsiConsole.Ask<string>("What is the description of the project?") ??
                        "A Default Zip project.";

                    return [
                        "const project = Manila.getProject()",
                        "const workspace = Manila.getWorkspace()",
                        "",
                        "project.version('1.0.0')",
                        $"project.description('{description}')",
                        "",
                        "project.sourceSets({",
                        "    main: Manila.sourceSet(project.getPath().join('main')).include('**/*')",
                        "})",
                        "",
                        "project.artifacts({",
                        "    main: Manila.artifact(artifact => {",
                        "        const config = Manila.getConfig(artifact)",
                        "",
                        "        Manila.job('build')",
                        "            .description('Create the Zip File')",
                        "            .execute(async () => {",
                        "                await Manila.build(workspace, project, config, artifact)",
                        "            })",
                        "    }).description('Zip Main Artifact')",
                        "})",
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
