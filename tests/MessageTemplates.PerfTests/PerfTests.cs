﻿using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.Tools.UnitTesting;

#if NO_MSTEST
namespace Microsoft.VisualStudio.Tools.UnitTesting
{
	class TestClass : Attribute { }
	public class TestContext {
		public string TestName { get; }
	}
	class TestInitialize : Attribute { }
	class TestCleanup : Attribute { }
	class TestMethod : Attribute { }
}
#endif

namespace MessageTemplates.PerfTests
{
    [TestClass]
    public class PerfTests
    {
        const int TEST_ITERATIONS = 2000;
        TimedWriteLine twl;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Init()
        {
            twl = new TimedWriteLine(Console.Out, TestContext.TestName);
        }

        [TestCleanup]
        public void CleanUp()
        {
            twl.Dispose();
        }

        class TimedWriteLine : IDisposable
        {
            readonly Stopwatch _sw = new Stopwatch();
            readonly string _testName;
            readonly System.IO.TextWriter output;

            public TimedWriteLine(System.IO.TextWriter output, string testName = null)
            {
                this.output = output;

                this._testName = testName ??
                    new StackTrace(1, fNeedFileInfo: false).GetFrame(0).GetMethod().Name;
                
                output.WriteLine(_testName);
                _sw.Start();
            }

            public void Dispose()
            {
                _sw.Stop();
                output.WriteLine("{0}\t{1}", _sw.Elapsed, _testName);
            }
        }

        static void Main(string[] args)
        {
            var t = new PerfTests();
            Action<Action> test = (action) =>
            {
                var name = action.Method.Name;
                action(); // warmup
                GC.Collect(3, GCCollectionMode.Forced, blocking: true);
                using (new TimedWriteLine(Console.Out, name))
                {
                    action();
                }
            };

            test(t.CSharpParseNamed);
            test(t.FSharpParseNamed);
            Console.WriteLine();
            test(t.CSharpParsePositional);
            test(t.FSharpParsePositional);
            Console.WriteLine();
            test(t.CSharpParseAndFormatNamed);
            test(t.FSharpParseAndFormatNamed);
            Console.WriteLine();
            test(t.CSharpParseAndFormatPositional);
            test(t.FSharpParseAndFormatPositional);
            Console.WriteLine();
            test(t.CSharpFormatPositional);
            test(t.FSharpFormatPositional);
            Console.WriteLine();
            test(t.CSharpParseAndFormatNamedDestr);
            test(t.FSharpParseAndFormatNamedDestr);
            Console.WriteLine();
            test(t.CSharpCaptureNamedDestr);
            test(t.FSharpCaptureNamedDestr);
        }

        readonly string[] NAMED_DESTR_TEMPLATES = new[]
        {
            "Hello, {@name}, how's {@fsprop} it {@going}?",
        };
        
        readonly object[][] NAMED_DESTR_ARGS = new[]
        {
            new object[] { "adam",
                new Chair(),
                FsMessageTemplates.Token.NewProp(1, new FsMessageTemplates.PropertyToken()),
                new Version(1,2,3,4),
            },
        };

        [TestMethod]
        public void FSharpCaptureNamedDestr()
        {
            var templates = NAMED_DESTR_TEMPLATES.Select(FsMessageTemplates.Parser.parse).ToArray();

            for (var i = 0; i < TEST_ITERATIONS * 3; i++)
            {
                for (var x = 0; x < templates.Length; x++)
                {
                    FsMessageTemplates.Capturing.captureProperties(templates[x], NAMED_DESTR_ARGS[x]);
                }
            }
        }

        [TestMethod]
        public void CSharpCaptureNamedDestr()
        {
            var templates = NAMED_DESTR_TEMPLATES.Select(MessageTemplate.Parse).ToArray();

            for (var i = 0; i < TEST_ITERATIONS * 3; i++)
            {
                for (var x = 0; x < templates.Length; x++)
                {
                    MessageTemplate.Capture(templates[x], NAMED_DESTR_ARGS[x]);
                }
            }
        }

        [TestMethod]
        public void FSharpParseAndFormatNamedDestr()
        {
            var tw = new System.IO.StringWriter();
            for (var _ = 0; _ < TEST_ITERATIONS * 3; _++)
            {
                for (int x = 0; x < NAMED_DESTR_TEMPLATES.Length; x++)
                {
                    FsMessageTemplates.Formatting.fprintsm(tw, NAMED_DESTR_TEMPLATES[x], NAMED_DESTR_ARGS[x]);
                }
            }
        }

        [TestMethod]
        public void CSharpParseAndFormatNamedDestr()
        {
            var tw = new System.IO.StringWriter();
            for (var i = 0; i < TEST_ITERATIONS*3; i++)
            {
                for (int x = 0; x < NAMED_DESTR_TEMPLATES.Length; x++)
                {
                    MessageTemplate.Format(tw.FormatProvider, tw, NAMED_DESTR_TEMPLATES[x], NAMED_DESTR_ARGS[x]);
                }
            }
        }

        readonly string[] NAMED_TEMPLATES = new[] {
            "Hello, {namsd,fgsdfg{{adam}}}, how's it {going1,-10:blah:bhal}? {going2:0,0} {going3:0,0} {going4:0,0} {going5:0,0} {going6:0,0}",
            "Welcome, customer #{CustomerId,-10}, pleasure to see you",
            "Welcome, customer #{CustomerId,-10:000000}, pleasure to see you",
            "Welcome, customer #{CustomerId,10}, pleasure to see you",
            "Welcome, customer #{CustomerId,10:000000}, pleasure to see you",
            "Welcome, customer #{CustomerId,10:0,0}, pleasure to see you",
            "Welcome, customer #{CustomerId:0,0}, pleasure to see you",
            "Welcome, customer #{CustomerId,-10}, pleasure to see you",
            "Welcome, customer #{CustomerId,-10:000000}, pleasure to see you",
            "Welcome, customer #{CustomerId,10}, pleasure to see you",
            "Welcome, customer #{CustomerId,10:000000}, pleasure to see you",
            "Welcome, customer #{CustomerId,10:0,0}, pleasure to see you",
            "Welcome, customer #{CustomerId:0,0}, pleasure to see you",
        };

        readonly string[] POSITIONAL_TEMPLATES = new[] {
            "Hello, {namsd,fgsdfg{{adam}}}, how's it {1,-10:blah:bhal}? {2:0,0} {3:0,0} {4:0,0} {5:0,0} {6:0,0}",
            "Welcome, customer #{1,-10}, pleasure to see you",
            "Welcome, customer #{3,-10:000000}, pleasure to see you",
            "Welcome, customer #{5,10}, pleasure to see you",
            "Welcome, customer #{2,10:000000}, pleasure to see you",
            "Welcome, customer #{4,10:0,0}, pleasure to see you",
            "Welcome, customer #{6:0,0}, pleasure to see you",
            "Welcome, customer #{2,-10}, pleasure to see you",
            "Welcome, customer #{1,-10:000000}, pleasure to see you",
            "Welcome, customer #{3,10}, pleasure to see you",
            "Welcome, customer #{5,10:000000}, pleasure to see you",
            "Welcome, customer #{6,10:0,0}, pleasure to see you",
            "Welcome, customer #{9:0,0}, pleasure to see you",
        };

        readonly object[][] ARGS = new[]
        {
            new object[] { 1, 2, 3, 4, 5, 6 },
            new object[] { 1, 2, 3, 4, 5, 6 },
            new object[] { 1 },
            new object[] { 1 },
            new object[] { 1 },
            new object[] { 1 },
            new object[] { 1 },
            new object[] { 1 },
            new object[] { 1 },
            new object[] { 1 },
            new object[] { 1 },
            new object[] { 1 },
            new object[] { 1 },
            new object[] { 1 },
        };

        private readonly IFormatProvider formatProvider = System.Globalization.CultureInfo.InvariantCulture;

        [TestMethod]
        public void CSharpParseNamed()
        {
            for (var i = 0; i < TEST_ITERATIONS; i++)
            {
                for (var x = 0; x < NAMED_TEMPLATES.Length; x++)
                {
                    MessageTemplates.MessageTemplate.Parse(NAMED_TEMPLATES[x]);
                }
            }
        }

        [TestMethod]
        public void CSharpParsePositional()
        {
            for (var i = 0; i < TEST_ITERATIONS; i++)
            {
                for (var x = 0; x < POSITIONAL_TEMPLATES.Length; x++)
                {
                    MessageTemplates.MessageTemplate.Parse(POSITIONAL_TEMPLATES[x]);
                }
            }
        }

        [TestMethod]
        public void FSharpParseNamed()
        {
            for (var i = 0; i < TEST_ITERATIONS; i++)
            {
                for (var x = 0; x < NAMED_TEMPLATES.Length; x++)
                {
                    FsMessageTemplates.Parser.parse(NAMED_TEMPLATES[x]);
                }
            }
        }

        [TestMethod]
        public void FSharpParsePositional()
        {
            for (var i = 0; i < TEST_ITERATIONS; i++)
            {
                for (var x = 0; x < POSITIONAL_TEMPLATES.Length; x++)
                {
                    FsMessageTemplates.Parser.parse(POSITIONAL_TEMPLATES[x]);
                }
            }
        }

        [TestMethod]
        public void CSharpParseAndFormatNamed()
        {
            for (var i = 0; i < TEST_ITERATIONS; i++)
            {
                for (var x = 0; x < NAMED_TEMPLATES.Length; x++)
                {
                    MessageTemplate.Format(formatProvider, NAMED_TEMPLATES[x], ARGS[x]);
                }
            }
        }

        [TestMethod]
        public void CSharpParseAndFormatPositional()
        {
            for (var i = 0; i < TEST_ITERATIONS; i++)
            {
                for (var x = 0; x < POSITIONAL_TEMPLATES.Length; x++)
                {
                    MessageTemplate.Format(formatProvider, POSITIONAL_TEMPLATES[x], ARGS[x]);
                }
            }
        }

        [TestMethod]
        public void FSharpParseAndFormatPositional()
        {
            for (var i = 0; i < TEST_ITERATIONS; i++)
            {
                for (var x = 0; x < POSITIONAL_TEMPLATES.Length; x++)
                {
                    var mt = FsMessageTemplates.Parser.parse(POSITIONAL_TEMPLATES[x]);
                    FsMessageTemplates.Formatting.format(mt, ARGS[x]);
                }
            }
        }

        [TestMethod]
        public void FSharpParseAndFormatNamed()
        {
            for (var i = 0; i < TEST_ITERATIONS; i++)
            {
                for (var x = 0; x < NAMED_TEMPLATES.Length; x++)
                {
                    var mt = FsMessageTemplates.Parser.parse(NAMED_TEMPLATES[x]);
                    FsMessageTemplates.Formatting.sprintm(mt, formatProvider, ARGS[x]);
                }
            }
        }

        [TestMethod]
        public void CSharpFormatPositional()
        {
            var templates = POSITIONAL_TEMPLATES.Select(MessageTemplate.Parse).ToArray();
            var tw = new System.IO.StringWriter();

            for (var i = 0; i < TEST_ITERATIONS; i++)
            {
                for (var x = 0; x < templates.Length; x++)
                {
                    templates[x].Format(tw.FormatProvider, tw, ARGS[x]);
                    tw.GetStringBuilder().Clear();
                }
            }
        }

        [TestMethod]
        public void FSharpFormatPositional()
        {
            var templates = POSITIONAL_TEMPLATES.Select(FsMessageTemplates.Parser.parse).ToArray();
            var tw = new System.IO.StringWriter();

            for (var i = 0; i < TEST_ITERATIONS; i++)
            {
                for (var x = 0; x < templates.Length; x++)
                {
                    FsMessageTemplates.Formatting.fprintm(templates[x], tw, ARGS[x]);
                    tw.GetStringBuilder().Clear();
                }
            }
        }

    }
}
