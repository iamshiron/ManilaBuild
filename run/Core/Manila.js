const project = Manila.getProject()
const workspace = Manila.getWorkspace()

Manila.apply('shiron.manila:cpp@1.0.0:staticlib')
const config = Manila.getConfig()

version('1.0.0')
description('Demo Project Core')

sourceSets({
	main: Manila.sourceSet(project.getPath().join('src/main')).include('**/*.cpp'),
	test: Manila.sourceSet(project.getPath().join('src/test')).include('**/*.cpp')
})

artifacts({
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
