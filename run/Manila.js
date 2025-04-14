const workspace = Manila.getWorkspace()

Manila.onProject('*', p => {
	p.setToolChain(ToolChain.Clang)
})

Manila.task('build').execute(() => {
	print('Building...')
})

Manila.task('run')
	.after('build')
	.execute(() => {
		print('Running...')
	})
