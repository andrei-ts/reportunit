﻿using System;
using System.Configuration;

namespace ReportUnit
{
    public static class Config
    {
        public static bool ParseScreenshots => Convert.ToBoolean(AppSetting("ParseScreenshots"));
        public static string ScreenshotsRootFolder => AppSetting("ScreenshotsRootFolder");
        public static string ScreenshotRegexp => AppSetting("ScreenshotRegexp");

        private static string AppSetting(string key)
        {
            return ConfigurationManager.AppSettings.Get(key) ?? string.Empty;
        }
    }
}