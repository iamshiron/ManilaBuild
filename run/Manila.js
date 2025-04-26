const workspace = Manila.getWorkspace()

print('Webhook:', Manila.getEnv('DISCORD_WEBHOOK_URL'))
print('Count:', Manila.getEnvNumber('COUNT'))
print('PI:', Manila.getEnvNumber('PI'))

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
