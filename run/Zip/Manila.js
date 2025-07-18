const project = Manila.getProject()
const workspace = Manila.getWorkspace()

Manila.apply('shiron.manila:zip@1.0.0:zip')
const config = Manila.getConfig()

version('1.0.0')
description('Demo Project Core')

config.setSubFolder('sub')

sourceSets({
	main: Manila.sourceSet(project.getPath().join('main')).include('**/*')
})

artifacts({
	main: Manila.artifact(() => {
		Manila.job('build')
			.description('Build the Core')
			.execute(() => {
				Manila.build(workspace, project, config, 'main')
			})
	})
		.from('shiron.manila:zip/zip')
		.description('Zip Main Artifact')
})
