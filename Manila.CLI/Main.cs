using Shiron.Manila;

#if DEBUG
Directory.SetCurrentDirectory("./run");
#endif

var engine = ManilaEngine.getInstance();
engine.runScript("Manila.js");
