const project = Manila.getProject()
const workspace = Manila.getWorkspace()
const config = Manila.getConfig()

Manila.apply('shiron.manila:cpp@1.0.0:staticlib')
version('1.0.0')
description('Demo Project Core')

binDir(workspace.getPath().join('bin', config.getPlatform(), `${config.getConfig()}-${config.getArchitecture()}`, project.getName()))
objDir(workspace.getPath().join('bin-int', config.getPlatform(), `${config.getConfig()}-${config.getArchitecture()}`, project.getName()))

sourceSets({
	main: Manila.sourceSet(project.getPath().join('src/main')).include('**/*.cpp'),
	test: Manila.sourceSet(project.getPath().join('src/test')).include('**/*.cpp')
})
