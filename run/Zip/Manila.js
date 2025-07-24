const project = Manila.getProject()
const workspace = Manila.getWorkspace()

Manila.apply('shiron.manila:zip@1.0.0/zip')
const config = Manila.getConfig()

project.version('1.0.0')
project.description('Demo Project Core')

config.setSubFolder(Manila.getEnv('MANILA_SUB_FOLDER', 'sub'))

project.sourceSets({
	main: Manila.sourceSet(project.getPath().join('main')).include('**/*')
})

project.artifacts({
	main: Manila.artifact(artifact => {
		Manila.job('build')
			.description('Create the Zip File')
			.execute(async () => {
				await Manila.build(workspace, project, config, artifact)
			})
	})
		.from('shiron.manila:zip/zip')
		.description('Zip Main Artifact')
})
