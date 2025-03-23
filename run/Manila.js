const workspace = Manila.getWorkspace()

Manila.project('*', p => {
	p.toolChain(ToolChain.Clang)
})

Manila.task('build').execute(() => {
	print('Building...')
})

Manila.task('run')
	.after('build')
	.execute(() => {
		print('Running...')
	})
