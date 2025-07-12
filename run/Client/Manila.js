const project = Manila.getProject()
const workspace = Manila.getWorkspace()
const config = Manila.getConfig()

Manila.apply('shiron.manila:cpp@1.0.0:console')

version('1.0.0')
description('Demo Project Core')

binDir(workspace.getPath().join('bin', config.getPlatform(), `${config.getConfig()}-${config.getArchitecture()}`, project.getName()))
objDir(workspace.getPath().join('bin-int', config.getPlatform(), `${config.getConfig()}-${config.getArchitecture()}`, project.getName()))
runDir(workspace.getPath().join('bin', config.getPlatform(), `${config.getConfig()}-${config.getArchitecture()}`, project.getName()))

sourceSets({
	main: Manila.sourceSet(project.getPath().join('src/main')).include('**/*.cpp'),
	test: Manila.sourceSet(project.getPath().join('src/test')).include('**/*.cpp')
})

dependencies([Manila.project('core', 'build')])

artifacts({
	app: Manila.artifact(() => {
		Manila.task('build-app')
			.description('Build app artifact')
			.execute(() => {
				Manila.build(workspace, project, config)
			})
	}).description('Main Artifact')
})

Manila.task('clean')
	.description('Clean the client')
	.execute(() => {
		print('Cleaning Client...')
	})

Manila.task('build').execute(() => {
	Manila.build(workspace, project, config)
})
Manila.task('build-test').execute(async () => {
	print('Building Test...')
	await Manila.sleep(5000)
	print('Done!')
})

Manila.task('test')
	.description('Run the Client Tests')
	.after('build-test')
	.execute(() => {
		print('Testing Client...')
	})
Manila.task('run')
	.description('Run the Client')
	.after('test')
	.after('build')
	.execute(() => {
		Manila.run(project)
	})
