const project = Manila.getProject()
const workspace = Manila.getWorkspace()

Manila.apply('shiron.manila:zip@1.0.0/zip')
const config = Manila.getConfig()

project.Version('1.0.0')
project.Description('Demo Project Core')

config.setSubFolder(Manila.getEnv('MANILA_SUB_FOLDER', 'sub'))

project.SourceSets({
	main: Manila.sourceSet(project.GetPath().join('main')).include('**/*')
})

project.Artifacts({
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
