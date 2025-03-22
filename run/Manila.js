const workspace = Manila.getWorkspace()

Manila.project('*', p => {
	p.toolChain(ToolChain.Clang)
})
