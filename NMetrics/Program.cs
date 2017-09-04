using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using QuickGraph;
using QuickGraph.Algorithms;
using QuickGraph.Graphviz;
using QuickGraph.Graphviz.Dot;

namespace NMetrics
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            MainMonexPay(args);
        }

        private static void MainThis(string[] args)
        {
            var path1 = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName;
            var mask = "*.dll;*.exe";
            var app = ScanDirectory(path1, mask).ToList();

            var repoInterfaces = app.GetTypes().InterfacesOnly().ToList();

            //var x = app.GetTypes("Test.*$").ClassesOnly().Usages();

        }

        private static IEnumerable<TypeDefinition> GetEntryPoints(List<AssemblyDefinition> app)
        {
            //webapi contollers;
            var webApiControllers = 
                app.GetTypes("System.*ApiController$")
                .Union(app.GetTypes("System.*.Web.*Controller$"))
                .ToList();
            var api = app.GetTypes().InheritedFrom(webApiControllers).ToList();
            return api;
        }

        private static void MainMonexPay(string[] args)
        {
            // var path1 = @"C:\Work\Monex\fxdb2\ossrc\Bin";
            var path1 = @"C:\Work\Monex\fxdb2\src\Bin";
            //var path2 = @"C:\Work\Monex\Monex Payroll\PayrollPoC\PayrollPoC\bin\Debug"\
            //var path1 = @"C:\Work\Monex\Monex Payroll\PayrollPoC\PayrollPoC\Bin\Debug";
            var mask = "*.dll;*.exe";
            //var assemblies = ScanDirectory(path1, mask)
            //    .Union(ScanDirectory(path2, mask))
            //    .DedupFiles();
            var app = ScanDirectory(path1, mask).ToList();

            Func<TypeReference, bool> filter = x => x.Namespace.StartsWith("Monex") || x.Namespace.Contains("FXDB");
            //Func<TypeReference, bool> filter = x => !x.Namespace.StartsWith("System");
            var repoInterfaces = app.GetTypes().InterfacesOnly().ToList();
            var monexTypes = app.GetTypes(".*$").Where(filter).Resolve().ToList(); 

            var entries = GetEntryPoints(app);
            var entriesUsage = entries.Usages(filter).ToList();
            var c1 = entriesUsage.Compact();

            var usage = monexTypes.ClassesOnly().Usages(filter).ToList();

            var interf = usage.Where(x => (x.UsageKind & UsageKind.Interface) > 0)
                .ToLookup(x => x.UsedType, x => x.UsingType)
                .SelectMany(x => x.Select(y => UsageInfo.ImplementedBy(x.Key.SmartResolve(), y)));

            usage.AddRange(interf);

            var distinctTypesRelations = usage.Compact();

            var g = new AdjacencyGraph<string, TaggedEdge<string, List<UsageInfo>>>(false);
            var g4 = new BidirectionalGraph<string, TaggedEdge<string, List<UsageInfo>>>(false);

            g.AddVerticesAndEdgeRange(distinctTypesRelations.Select(x => new TaggedEdge<string, List<UsageInfo>>(x.Source, x.Target, x.Usage)));
            g4.AddVerticesAndEdgeRange(distinctTypesRelations.Select(x => new TaggedEdge<string, List<UsageInfo>>(x.Source, x.Target, x.Usage)));
            g.TrimEdgeExcess();

            var allPaths = new List<IEnumerable<TypeReference>>();
            var allPathStrings = new List<string>();
            var g2 = new AdjacencyGraph<string, Edge<string>>(false);

            var sourceTypes = monexTypes.Filtered(".*Service$")
                .Union(monexTypes.Filtered(".*Controller"))
                .ClassesOnly()
                .Select(x => x.FullName)
                .ToHashSet();

            var alllll = monexTypes.IncludingNested().ToList();

            var targetTypes = monexTypes
                //.Filtered("F.*Context$")
                .IncludingNested()
                .Filtered("Netdania")
                .ClassesOnly()
                .Select(x => x.FullName)
                .ToHashSet();

            foreach (var controller in g.Vertices.Where(x => sourceTypes.Contains(x)))
            {
                var pathFromContoller = g.ShortestPathsDijkstra(x => 1, controller);
                foreach (var repo in g.Vertices.Where(x => targetTypes.Contains(x)))
                {
                    IEnumerable<TaggedEdge<string, List<UsageInfo>>> path;
                    if (pathFromContoller(repo, out path))
                    {
                        var hops = path.Where(x => (x.Tag.First().UsageKind & UsageKind.InterfaceImplementation) == 0)
                            .Select(x => x.Tag.First().UsingType)
                            .Concat(Enumerable.Repeat(path.Last().Tag.First().UsedType, 1))
                            .ToList();

                        allPaths.Add(hops);
                        foreach (var segment in path)
                        {
                            g2.AddVerticesAndEdge(new Edge<string>(segment.Tag.First().UsingType.GetShortName(), segment.Tag.First().UsedType.GetShortName()));
                        }
                    }
                }
            }
            g2.TrimEdgeExcess();

            foreach (var path in allPaths)
            {
                var pathString = string.Join(" -> ", path.Select(x => x.GetShortName()));
                allPathStrings.Add(pathString);
            }

            var typesToKeep = monexTypes.Filtered(".*Program$")
                .Union(monexTypes.Filtered(".*Context$"))
                .Union(monexTypes.Filtered(".*Global.*$"))
                .Union(monexTypes.Filtered(".*Repository.*$"))
                .Union(monexTypes.Filtered(".*FxdbModel.*$"))
                .Select(x => x.FullName);

            var g3 = new AdjacencyGraph<string, TaggedEdge<string, List<UsageInfo>>>(false);
            g.BuildSubGraph(g3, typesToKeep, (x, y) => new TaggedEdge<string, List<UsageInfo>>(x, y, null));

            // var graphviz = new GraphvizAlgorithm<string, Edge<string>>(g3);
            var graphviz = new GraphvizAlgorithm<string, TaggedEdge<string, List<UsageInfo>>>(g3);
            graphviz.FormatVertex += Graphviz_FormatVertex;

            // render
            string output = graphviz.Generate();


            foreach (var pathString in allPathStrings.Distinct().OrderBy(x => x).ThenBy(x => x.Length))
            {
                Console.WriteLine(pathString);
            }
            var dbContextTypes = app.GetTypes("FxdbContext$").ToList();
            var repoTypes = app.GetTypes("Repository$").ToList();
        }

        private static void Graphviz_FormatVertex(object sender, FormatVertexEventArgs<string> e)
        {
            //throw new NotImplementedException();
        }

        private static IEnumerable<UsageInfo> Filter(List<AssemblyDefinition> assemblies, List<UsageInfo> usages,
            string filter)
        {
            var usingTypes = assemblies.GetTypes(filter).ClassesOnly().ToList();
            var inheritedTypes = assemblies.GetTypes().InheritedFrom(usingTypes).ToList();
            var allTypes = usingTypes.Concat(inheritedTypes).Distinct().ToList();
            var filtered = usages.Where(x => allTypes.Contains(x.UsingType)).ToList();
            return filtered;
        }

        private static void Dump(IEnumerable<UsageInfo> usages, string layer)
        {
            foreach (var uu in usages)
            {
                Console.WriteLine(
                    $"{uu.UsingType.FullName}, {uu.UsingMethod.Name}, {layer}, {uu.UsedType.Name}, {uu.UsedMethod?.Name ?? "_"}, {uu.UsageKind}");
            }
        }

        private static ICollection<AssemblyDefinition> ScanDirectory(string path, string mask)
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(path);

            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadingMode = ReadingMode.Deferred
            };
            var assemblies = new List<AssemblyDefinition>();

            AppendDirectory(assemblies, path, mask, readerParameters);
            return assemblies;
        }

        private static void AppendDirectory(ICollection<AssemblyDefinition> assemblies, string path, string mask,
            ReaderParameters param)
        {
            var masks = mask?.Split(';') ?? new[] { string.Empty };
            if (!new DirectoryInfo(path).Exists)
            {
                return;
            }
            foreach (var singleMask in masks)
            {
                var files = Directory.EnumerateFiles(path, singleMask, SearchOption.AllDirectories).ToList();

                foreach (var fname in files)
                {
                    AppendAssembly(assemblies, fname, param);
                }
            }
        }

        private static bool AppendAssembly(ICollection<AssemblyDefinition> collection, string fname,
            ReaderParameters param)
        {
            try
            {
                var def = AssemblyDefinition.ReadAssembly(fname, param);
                if (collection.Any(x => x.FullName == def.FullName))
                {
                    return false;
                }
                collection.Add(def);
                return true;
            }
            catch
            {
                //assembly may not load for various reasons.
                return false;
            }
        }

        public class FileDotEngine : IDotEngine
        {
            public string Run(GraphvizImageType imageType, string dot, string outputFileName)
            {
                throw new NotImplementedException();
            }
        }
    }
}