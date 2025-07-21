const project = Manila.getProject()
const workspace = Manila.getWorkspace()

Manila.apply('shiron.manila:cpp@1.0.0:staticlib')
const config = Manila.getConfig()

project.Version('1.0.0')
project.Description('Demo Project Core')

project.SourceSets({
	main: Manila.sourceSet(project.GetPath().join('src/main')).include('**/*.cpp'),
	test: Manila.sourceSet(project.GetPath().join('src/test')).include('**/*.cpp')
})

project.Artifacts({
	main: Manila.artifact(() => {
		Manila.job('build')
			.description('Build the Core')
			.execute(() => {
				Manila.build(workspace, project, config)
			})
	})
		.from('shiron.manila:cpp/console')
		.description('Core Main Artifact')
})
