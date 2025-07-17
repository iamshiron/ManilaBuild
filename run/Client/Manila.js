const project = Manila.getProject()
const workspace = Manila.getWorkspace()

Manila.apply('shiron.manila:cpp@1.0.0:console')
const config = Manila.getConfig()

version('1.0.0')
description('Demo Project Core')

sourceSets({
	main: Manila.sourceSet(project.getPath().join('src/main')).include('**/*.cpp'),
	test: Manila.sourceSet(project.getPath().join('src/test')).include('**/*.cpp')
})

artifacts({
	main: Manila.artifact(() => {
		Manila.job('clean')
			.description('Clean the client')
			.execute(() => {
				print('Cleaning Client...')
			})

		Manila.job('build').execute(() => {
			print('Building client...')
		})

		Manila.job('run')
			.description('Run the Client')
			.after('build')
			.execute(() => {
				print('Running client...')
			})
	})
		.from('shiron.manila:cpp/console')
		.description('Client Main Artifact')
})
