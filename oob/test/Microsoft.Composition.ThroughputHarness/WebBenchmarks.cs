﻿using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Hosting;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CompositionThroughput
{
    abstract class WebBenchmark : Benchmark
    {
        public override Action GetOperation()
        {
            var op = GetCompositionOperation();
            return () => op().Item2();
        }

        public override bool SelfTest()
        {
            var op = GetCompositionOperation();
            var r1 = op();

            if (!(r1.Item1 is Web.OperationRoot))
                return false;

            r1.Item2();
            var or1 = (Web.OperationRoot)r1.Item1;
            if (!(or1.Long.TailA.IsDisposed &&
                or1.Long.TailA.TailB.IsDisposed &&
                or1.Long.TailA.TailB.TailC.IsDisposed))
                return false;

            if (or1.Wide.A1 != or1.Wide.A2)
                return false;

            var r2 = op();
            if (r2.Item1 == r1.Item1)
                return false;

            var or2 = (Web.OperationRoot)r2.Item1;
            if (or2.Long.TailA.IsDisposed)
                return false;

            if (or1.Wide.A1 == or2.Wide.A1)
                return false;

            if (or1.Wide.A1.GA != or2.Wide.A1.GA)
                return false;

            r2.Item2();
            return true;
        }

        public abstract Func<Tuple<object, Action>> GetCompositionOperation();
    }

    class LightweightWebBenchmark : WebBenchmark
    {
        [Export]
        class WebServer
        {
            [Import, SharingBoundary(Web.Boundaries.Web)]
            public ExportFactory<Web.OperationRoot> WebScopeFactory { get; set; }
        }

        public override Func<Tuple<object, Action>> GetCompositionOperation()
        {
            var container = new ContainerConfiguration()
                .WithParts(new[]{
                    typeof(WebServer),
                    typeof(Web.OperationRoot),
                    typeof(Web.GlobalA),
                    typeof(Web.GlobalB),
                    typeof(Web.Transient),
                    typeof(Web.Wide),
                    typeof(Web.A),
                    typeof(Web.B),
                    typeof(Web.Long),
                    typeof(Web.TailA),
                    typeof(Web.TailB),
                    typeof(Web.TailC)})
                .CreateContainer();

            var sf = container.GetExport<WebServer>().WebScopeFactory;
            return () => {
                var x = sf.CreateExport();
                return Tuple.Create<object, Action>(x.Value, x.Dispose);
            };
        }
    }

    class NativeCodeWebBenchmark : WebBenchmark
    {
        public override Func<Tuple<object, Action>> GetCompositionOperation()
        {
            var globalA = new Web.GlobalA();
            var globalB = new Web.GlobalB();
            return () =>
            {
                var tc = new Web.TailC();
                var tb = new Web.TailB(tc);
                var ta = new Web.TailA(tb);
                var a = new Web.A(globalA);
                var b = new Web.B(globalB);
                var transient = new Web.Transient();
                var w = new Web.Wide(a, a, b, transient);
                var l = new Web.Long(ta);
                var r = new Web.OperationRoot(w, l);
                return Tuple.Create<object, Action>(r, () => { ta.Dispose(); tb.Dispose(); tc.Dispose(); });
            };
        }
    }

    static class CompositionScope
    {
        public const string Global = "Global";
    }
}
