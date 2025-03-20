const workspace = Manila.getWorkspace()

// Manila.task('run')
// 	.after(':build')
// 	.execute(() => {
// 		Manila.run(Manila.getProject(':server'))
// 	})

// Manila.task('build').execute(() => {
// 	Manila.build(workspace, Manila.getProject(':server'), config)
// })

print('Core Version:')
print(Manila.getProject(':core').resolve().version())
