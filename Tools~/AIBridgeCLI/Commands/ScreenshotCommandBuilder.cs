using System.Collections.Generic;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// Screenshot command builder: capture Game view screenshots and GIF recordings
    /// </summary>
    public class ScreenshotCommandBuilder : BaseCommandBuilder
    {
        public override string Type => "screenshot";
        public override string Description => "Capture screenshots and GIF recordings (Game view)";

        public override string[] Actions => new[]
        {
            "game",
            "gif"
        };

        protected override Dictionary<string, List<ParameterInfo>> ActionParameters => new Dictionary<string, List<ParameterInfo>>
        {
            ["game"] = new List<ParameterInfo>(),
            ["gif"] = new List<ParameterInfo>
            {
                new ParameterInfo("frameCount", "Number of frames to capture (1-200)", true),
                new ParameterInfo("fps", "Frames per second (10-30)", false, "20"),
                new ParameterInfo("scale", "Resolution scale factor (0.25-1.0)", false, "0.5"),
                new ParameterInfo("colorCount", "GIF palette color count (64-256)", false, "256")
            }
        };
    }
}
