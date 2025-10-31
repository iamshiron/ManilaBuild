var project = Manila.GetProject();
var workspace = Manila.GetWorkspace();

project.Version("1.0.0");
project.Description("SimpleJS Project");

project.SourceSets({
	main: Manila.SourceSet(project.GetPath().Join("src")).Include("**/*"),
});

project.Artifacts({
	main: Manila.Artifact("shiron.manila:js/js", (artifact) => {
		var config = Manila.GetConfig(artifact);
		config.SetRuntime("Node");

		artifact.Description("Simple JS artifac");
		artifact.Dependencies([
		]);

		Manila.Job("build")
			.Description("Build the SimpleJS Application")
			.Execute(async () => {
                // Does nothing for now
			});

        Manila.Job("run")
            .Description("Run the SimpleJS Application")
            .Execute(async () => {
                await Manila.Run(project, config, artifact);
            });
	}),
});
