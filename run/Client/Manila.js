const project = Manila.getProject()
const workspace = Manila.getWorkspace()
const config = Manila.getConfig()

Manila.apply('shiron.manila:cpp@1.0.0:console')

version('1.0.0')
description('Demo Project Core')

binDir(workspace.path().join('bin', config.getPlatform(), `${config.getConfig()}-${config.getArchitecture()}`, project.name()))
objDir(workspace.path().join('bin-int', config.getPlatform(), `${config.getConfig()}-${config.getArchitecture()}`, project.name()))
runDir(workspace.path().join('bin', config.getPlatform(), `${config.getConfig()}-${config.getArchitecture()}`, project.name()))

sourceSets({
	main: Manila.sourceSet(project.path().join('src/main')).include('**/*.cpp'),
	test: Manila.sourceSet(project.path().join('src/test')).include('**/*.cpp')
})

Manila.task('build').execute(() => {
	print('Building Client...')
})
Manila.task('test')
	.after('build')
	.execute(() => {
		print('Testing Client...')
	})
Manila.task('run')
	.after('test')
	.after('build')
	.execute(() => {
		print('Running Client...')
	})
