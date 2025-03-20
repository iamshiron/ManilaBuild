const workspace = Manila.getWorkspace()
const config = Manila.getBuildConfig()

Manila.task('run')
	.after(':build')
	.execute(() => {
		Manila.run(Manila.getProject(':server'))
	})

Manila.task('build').execute(() => {
	Manila.build(workspace, Manila.getProject(':server'), config)
})
