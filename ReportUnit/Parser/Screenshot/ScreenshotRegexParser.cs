using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text.RegularExpressions;

namespace ReportUnit.Parser.Screenshot
{
    public class ScreenshotRegexParser : IScreenshotParser
    {
        private readonly string _input;

        public IList<string> ScreenshotLinks { get; }

        public ScreenshotRegexParser(string input)
        {
            ScreenshotLinks = new List<string>();
            _input = input;
        }

        public void Parse()
        {
            MatchCollection matches = Regex.Matches(_input.Trim(),Config.ScreenshotRegexp);
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    ScreenshotLinks.Add($"<a href='{Path.Combine(Config.ScreenshotsRootFolder,match.Groups[1].Value)}'>{match.Groups[1].Value}</a>");
                }
            }
        }
    }
}
