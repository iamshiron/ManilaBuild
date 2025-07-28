const project = Manila.getProject()
const workspace = Manila.getWorkspace()

project.version('1.0.0')
project.description('Demo Project Core')

project.sourceSets({
	main: Manila.sourceSet(project.getPath().join('main')).include('**/*')
})

project.artifacts({
	main: Manila.artifact('shiron.manila:zip/zip', artifact => {
		const config = Manila.getConfig(artifact)
		config.setSubFolder(Manila.getEnv('MANILA_SUB_FOLDER', 'sub'))

		Manila.job('build')
			.description('Create the Zip File')
			.execute(async () => {
				await Manila.build(project, config, artifact)
			})
	}).description('Zip Main Artifact')
})
