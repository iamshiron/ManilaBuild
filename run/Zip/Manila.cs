var project = Manila.GetProject();
var workspace = Manila.GetWorkspace();

project.Version("1.0.0");
project.Description("Demo Project Core");

project.SourceSets(new Dictionary<string, object> {
    ["main"] = Manila.SourceSet(project.GetPath().Join("main")).Include("**/*")
});

project.Artifacts(new Dictionary<string, object> {
    ["main"] = Manila.Artifact("shiron.manila:zip/zip", artifact => {
        var config = Manila.GetConfig<ZipBuildConfig>(artifact);
        config.SetSubFolder(Manila.GetEnv("MANILA_SUB_FOLDER", "sub"));

        Manila.Job("build")
            .Description("Create the Zip File")
            .Execute(async () => {
                await Manila.Build(project, config, artifact);
            });
    }).Description("Zip Main Artifact")
});
