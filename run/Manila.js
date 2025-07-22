const workspace = Manila.getWorkspace()
const Webhook = Manila.import('shiron.manila:discord/webhook')

const hook = Webhook.create(Manila.getEnv('DISCORD_WEBHOOK_URL'))
await hook.send('Hello from Manila!')

print('Hello from Manila!')
if (Manila.getEnvBool('ENABLE')) {
	print('Enabled')
} else {
	print('Disabled')
}

Manila.job('build').execute(() => {
	print('Building...')
})

Manila.job('run')
	.after('build')
	.execute(() => {
		print('Running...')
	})

Manila.job('shell').after('build').execute(Manila.shell('echo From Shell!'))
Manila.job('chained')
	.after('build')
	.execute([Manila.shell('echo One'), Manila.shell('echo Two'), Manila.shell('echo Three')])

Manila.job('sleep').execute(async () => {
	print('Sleeping for 2 seconds...')
	await Manila.sleep(2000)
	print('Done sleeping!')
})
