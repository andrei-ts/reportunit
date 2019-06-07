using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ReportUnit.Logging;
using ReportUnit.Model;
using ReportUnit.Parser.Screenshot;
using ReportUnit.Utils;

namespace ReportUnit.Parser
{
    public class TestNg : IParser
    {
        public TestNg() { }

        private string resultsFile;

        private Logger logger = Logger.GetLogger();

        public Report Parse(string resultsFile)
        {
            this.resultsFile = resultsFile;

            XDocument doc = XDocument.Load(resultsFile);

            Report report = new Report();

            report.FileName = Path.GetFileNameWithoutExtension(resultsFile);
            report.AssemblyName = null;
            report.TestRunner = TestRunner.TestNg;

            // run-info & environment values -> RunInfo
            var runInfo = CreateRunInfo(doc, report);
            if (runInfo != null)
            {
                report.AddRunInfo(runInfo.Info);
            }           
            // report counts
            report.Total =
                doc.Root.Attribute("total") != null
                    ? Int32.Parse(doc.Root.Attribute("total").Value)
                    : 0;


            report.Passed =
                doc.Root.Attribute("passed") != null
                    ? Int32.Parse(doc.Root.Attribute("passed").Value)
                    : 0;

            report.Failed =
                doc.Root.Attribute("failed") != null
                    ? Int32.Parse(doc.Root.Attribute("failed").Value)
                    : 0;

            report.Skipped =
                doc.Root.Attribute("skipped") != null
                    ? Int32.Parse(doc.Root.Attribute("skipped")?.Value)
                    : 0;

            report.Skipped +=
                doc.Root.Attribute("ignored") != null
                    ? Int32.Parse(doc.Root.Attribute("ignored")?.Value)
                    : 0;

            // report duration
            report.StartTime = runInfo.Info["Start time"];

            report.EndTime = runInfo.Info["End time"];

            // report status messages
            var testSuiteTypeAssembly = "testSuiteTypeAssembly";


            var parentSuites = doc.Descendants("suite");

            parentSuites.AsParallel().AsOrdered().Where(parentTs => parentTs.Descendants("test").Any()).ToList().ForEach(parentTs =>
            {
                var parentTestSuite = new ParentTestSuite();
                parentTestSuite.Name = parentTs.Attribute("name")?.Value;

                // Parent Suite Time Info
                parentTestSuite.StartTime =
                    parentTs.Attribute("started-at") != null
                        ? parentTs.Attribute("started-at")?.Value
                        : string.Empty;

                parentTestSuite.EndTime =
                    parentTs.Attribute("finished-at") != null
                        ? parentTs.Attribute("finished-at")?.Value
                        : string.Empty;

                string parentSuiteDurationAsString = parentTs.Attribute("duration-ms")?.Value;
                double parentSuiteSeconds = Convert.ToDouble(parentSuiteDurationAsString);
                parentTestSuite.Duration = parentSuiteSeconds;

                IEnumerable<XElement> suites = parentTs
                .Descendants("test");
                suites.AsParallel().AsOrdered().Where(suite => suite.Descendants("test-method").Any()).ToList().ForEach(ts =>
                {
                    var testSuite = new TestSuite();
                    testSuite.Name = ts.Attribute("name")?.Value;

                    // Suite Time Info
                    testSuite.StartTime =
                        ts.Attribute("started-at") != null
                            ? ts.Attribute("started-at")?.Value
                            : string.Empty;

                    // Suite Time Info
                    testSuite.EndTime =
                        ts.Attribute("finished-at") != null
                            ? ts.Attribute("finished-at")?.Value
                            : string.Empty;

                    string durationAsString = ts.Attribute("duration-ms")?.Value;
                    double seconds = Convert.ToDouble(durationAsString);
                    testSuite.Duration = seconds;

                    // Test Cases
                    ts.Descendants("test-method").AsParallel().AsOrdered().ToList().ForEach(tc =>
                    {
                        var test = new Model.Test { MethodName = tc.Attribute("name")?.Value };
                        var parameterElements = tc.Descendants("param").ToList();

                        test.MethodName += parameterElements.Any()
                            ? "(" + string.Join(",", parameterElements.Select(pEl => pEl.Element("value")?.Value)) + ")"
                            : "()";
                        test.Name = tc.Attribute("name")?.Value;
                        test.Status = (tc.Attribute("status")?.Value).ToStatus();

                        // main a master list of all status
                        // used to build the status filter in the view
                        report.StatusList.Add(test.Status);

                        // TestCase Time Info
                        test.StartTime =
                            tc.Attribute("started-at") != null
                                ? tc.Attribute("started-at")?.Value
                                : "";

                        test.EndTime =
                            tc.Attribute("finished-at") != null
                                ? tc.Attribute("finished-at")?.Value
                                : "";
                        //duration

                        string duration = tc.Attribute("duration-ms") != null ? tc.Attribute("duration-ms")?.Value : "";
                        if (!string.IsNullOrEmpty(duration))
                        {
                            TimeSpan t = TimeSpan.FromMilliseconds(Convert.ToDouble(duration));
                            test.Duration = t.ToString(@"hh\:mm\:ss\:fff");
                        }


                        string delimeter = Environment.NewLine + "====================================================" +
                                           Environment.NewLine;


                        // add TestNG console output to the status message
                        var reporterOutputElement = tc.Element("reporter-output");

                        if (reporterOutputElement != null)
                        {
                            var reporterLinesElements = reporterOutputElement.Descendants("line").ToList();
                            if (reporterLinesElements.Any())
                            {
                                test.StatusMessage += delimeter + "EXECUTE STEPS:" + delimeter;
                                foreach (var reporterLineElement in reporterLinesElements)
                                {
                                    var logLine = reporterLineElement.Value.Trim();
                                    test.StatusMessage += logLine + delimeter;
                                    //screenshots
                                    if (Config.ParseScreenshots)
                                    {
                                        var screenshotParser = new ScreenshotRegexParser(logLine);
                                        screenshotParser.Parse();
                                        test.ScreenshotLinks.AddRange(screenshotParser.ScreenshotLinks);
                                    }
                                }


                            }
                        }

                        // error and other status messages
                        var exceptionElement = tc.Element("exception");
                        if (exceptionElement != null)
                        {
                            var messageElement = exceptionElement.Element("message");
                            if (messageElement != null)
                            {
                                test.StatusMessage += delimeter + "EXCEPTION MESSAGE: " + Environment.NewLine +
                                                     messageElement.Value.Trim();
                            }

                            var stackTraceElement = exceptionElement.Element("full-stacktrace");
                            if (stackTraceElement != null)
                            {
                                test.StatusMessage += delimeter + "EXCEPTION STACKTRACE:" + Environment.NewLine +
                                                      stackTraceElement.Value.Trim();
                            }
                        }

                        testSuite.TestList.Add(test);

                    });

                    testSuite.Status = ReportUtil.GetFixtureStatus(testSuite.TestList);
                    parentTestSuite.TestSuiteList.Add(testSuite);
                });
                report.ParentTestSuiteList.Add(parentTestSuite);
            });
        


            

            return report;
        }

        /// <summary>
        /// Returns categories for the direct children or all descendents of an XElement
        /// </summary>
        /// <param name="elem">XElement to parse</param>
        /// <param name="allDescendents">If true, return all descendent categories.  If false, only direct children</param>
        /// <returns></returns>
        private HashSet<string> GetCategories(XElement elem)
        {
            //Grab unique categories
            HashSet<string> categories = new HashSet<string>();

            var propertiesElement = elem.Elements("properties").ToList();
            if (!propertiesElement.Any())
            {
                return categories;
            }
            //get all <property name="Category"> elements
            var categoryProperties = propertiesElement.Elements("property")
                .Where(c =>
                {
                    var xAttribute = c.Attribute("name");
                    return xAttribute != null &&
                           xAttribute.Value.Equals("Category", StringComparison.CurrentCultureIgnoreCase);
                })
                .ToList().ToList();
            if (!categoryProperties.Any())
            {
                return categories;
            }
            categoryProperties.ForEach(x =>
            {
                var xAttribute = x.Attribute("value");
                if (xAttribute != null)
                {
                    string cat = xAttribute.Value;
                    categories.Add(cat);
                }
            });
            return categories;
        }

        private RunInfo CreateRunInfo(XDocument doc, Report report)
        {
            RunInfo runInfo = new RunInfo();
            if (doc.Descendants("suite").Any())
            {
                XElement testRun = doc.Descendants("suite").First();

                if (testRun.Attribute("started-at") != null)
                    runInfo.Info.Add("Start time", testRun.Attribute("started-at").Value);

                if (testRun.Attribute("finished-at") != null)
                    runInfo.Info.Add("End time", testRun.Attribute("finished-at").Value);

                if (testRun.Attribute("duration-ms") != null)
                {
                    string durationAsString = testRun.Attribute("duration-ms").Value;
                    double seconds = Convert.ToDouble(durationAsString);
                    TimeSpan timeSpan = TimeSpan.FromMilliseconds(seconds);
                    string time = timeSpan.ToString(@"hh\h\:mm\m\:ss\s\:fff\m\s");

                    runInfo.Info.Add("Duration", time);
                }
            }
            return runInfo;
        }
    }
}
