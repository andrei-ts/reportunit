using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReportUnit.Model
{
    public class ParentTestSuite
    {
        public ParentTestSuite()
        {
            TestSuiteList = new List<TestSuite>();
            Status = Status.Unknown;
        }

        public List<TestSuite> TestSuiteList { get; set; }

        public string Name { get; set; }

        public string StartTime { get; set; }

        public string EndTime { get; set; }

        /// <summary>
        /// How long the test fixture took to run (in milliseconds)
        /// </summary>
        public double Duration { get; set; }

        public Status Status { get; set; }
    }
}
