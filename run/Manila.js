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

Manila.job('forever')
	.execute(async () => {
		print('0')
		await Manila.sleep(1000)
		print('1')
		await Manila.sleep(1000)
		print('2')
		await Manila.sleep(1000)
		print('3')
		await Manila.sleep(1000)
		print('4')
		await Manila.sleep(1000)
		print('5')
		await Manila.sleep(1000)
		print('6')
		await Manila.sleep(1000)
		print('7')
	})
	.background()

Manila.job('run')
	.after('build')
	.after('forever')
	.execute(() => {
		print('Running...')
	})

Manila.job('send')
	.execute(async () => {})
	.background()

Manila.job('shell').after('build').execute(Manila.shell('echo From Shell!'))
Manila.job('chained')
	.after('build')
	.execute([Manila.shell('echo One'), Manila.shell('echo Two'), Manila.shell('echo Three')])

Manila.job('sleep').execute(async () => {
	print('Sleeping for 2 seconds...')
	await Manila.sleep(2000)
	print('Done sleeping!')
})
