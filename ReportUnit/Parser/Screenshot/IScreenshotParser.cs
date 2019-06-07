using System.Collections.Generic;

namespace ReportUnit.Parser.Screenshot
{
    public interface IScreenshotParser
    {
        IList<string> ScreenshotLinks { get; }
        void Parse();
    }
}