const workspace = Manila.getWorkspace()
const Webhook = Manila.import('shiron.manila:discord/webhook')

print(Webhook)
const hook = Webhook.create(Manila.getEnv('DISCORD_WEBHOOK_URL'))
hook.send('Hello from Manila!')

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
