using Shiron.Manila.API;

namespace Shiron.Manila.Zip.Templates;

public static class DefaultTemplate {
    public static ProjectTemplate Create() {
        return new ProjectTemplateBuilder("default", "Default Zip Template")
            .WithFile(
                new TemplateFileBuilder("/Manila.js", (args) => {
                    return [
                        "const project = Manila.getProject()",
                        "const workspace = Manila.getWorkspace()",
                        "",
                        $"Manila.apply('shiron.manila:zip@{ManilaZip.Instance!.Version}/zip')",
                        "const config = Manila.getConfig()",
                        "",
                        "project.version('1.0.0')",
                        $"project.description('{args["description"]!}')",
                        "",
                        "project.sourceSets({",
                        "    main: Manila.sourceSet(project.getPath().join('main')).include('**/*')",
                        "})",
                        "",
                        "project.artifacts({",
                        "    main: Manila.artifact(artifact => {",
                        "        Manila.job('build')",
                        "            .description('Create the Zip File')",
                        "            .execute(async () => {",
                        "                await Manila.build(workspace, project, config, artifact)",
                        "            })",
                        "    })",
                        "        .from('shiron.manila:zip/zip')",
                        "        .description('Zip Main Artifact')",
                        "})",
                    ];
                })
            )
            .WithProperty("description", "The description of the project", false, "Manila Zip Project")
        .Build();
    }
}
