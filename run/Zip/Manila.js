var project = Manila.GetProject();
var workspace = Manila.GetWorkspace();

project.Version("1.0.0");
project.Description("Demo Project Core");

project.SourceSets({
	main: Manila.SourceSet(project.GetPath().Join("main")).Include("**/*"),
});

project.Artifacts({
	main: Manila.Artifact("shiron.manila:zip/zip", (artifact) => {
		var config = Manila.GetConfig(artifact);
		config.SetSubFolder(Manila.GetEnv("MANILA_SUB_FOLDER", "sub"));

		artifact.Description("Zip Main Artifact");
		artifact.Dependencies([Manila.Artifact(Manila.GetProject("zip2"), "main")]);

		Manila.Job("build")
			.Description("Create the Zip File")
			.Execute(async () => {
				await Manila.Build(project, config, artifact);
			});
	}),
});
