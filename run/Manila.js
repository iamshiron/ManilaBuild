const workspace = Manila.getWorkspace()

if (Manila.getEnvBool('ENABLE')) {
	print('Enabled')
} else {
	print('Disabled')
}

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
