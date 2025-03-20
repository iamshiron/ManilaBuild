using Shiron.Manila;
using Shiron.Manila.Utils;

#if DEBUG
Directory.SetCurrentDirectory("./run");
#endif

Logger.init(true, false);

var engine = ManilaEngine.getInstance();
engine.run();
