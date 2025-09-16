var workspace = Manila.GetWorkspace();

//var hook = new Webhook(Manila.GetEnv("DISCORD_WEBHOOK_URL"));
//await hook.Send("Hello from Manila!");

Manila.Log("Hello from Manila!");
if (Manila.GetEnvBool("ENABLE")) {
	Manila.Log("Enabled");
} else {
	Manila.Log("Disabled");
}

Manila.Job("build").Execute(() => {
	Manila.Log("Building...");
});

Manila.Job("forever")
	.Execute(async () => {
		Manila.Log("0");
		await Manila.Sleep(1000);
		Manila.Log("1");
		await Manila.Sleep(1000);
		Manila.Log("2");
		await Manila.Sleep(1000);
		Manila.Log("3");
		await Manila.Sleep(1000);
		Manila.Log("4");
		await Manila.Sleep(1000);
		Manila.Log("5");
		await Manila.Sleep(1000);
		Manila.Log("6");
		await Manila.Sleep(1000);
		Manila.Log("7");
	})
	.Background();

Manila.Job("run")
	.After("build")
	.After("forever")
	.Execute(() => {
		Manila.Log("Running...");
	});

Manila.Job("send")
	.Execute(async () => {})
	.Background();

Manila.Job("Shell").After("build").Execute(Manila.Shell("echo From Shell!"));
Manila.Job("chained")
	.After("build")
	.Execute([
		Manila.Shell("echo One"),
		Manila.Shell("echo Two"),
		Manila.Shell("echo Three"),
	]);

Manila.Job("sleep").Execute(async () => {
	Manila.Log("Sleeping for 2 seconds...");
	await Manila.Sleep(2000);
	Manila.Log("Done Sleeping!");
});
