using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using NMetrics.Introspection;
using NMetrics.Relations;
using QuickGraph;
using QuickGraph.Algorithms;
using QuickGraph.Graphviz;
using QuickGraph.Graphviz.Dot;
using MoreLinq;

namespace NMetrics
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var path1 = @"C:\Work\Monex\fxdb2\ossrc\Bin";
            //var path1 = @"C:\Work\Monex\fxdb2\src\Bin";
            //var path1 = @"C:\Work\Research\DotNextDemo\DotNextRZD.PublicApi\bin\";

            //var path2 = @"C:\Work\Monex\Monex Payroll\PayrollPoC\PayrollPoC\bin\Debug"\
            //var path1 = @"C:\Work\Monex\Monex Payroll\PayrollPoC\PayrollPoC\Bin\Debug";
            var path2 = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName;
            Build(path1);
        }

        private static void Build(string path)
        {
            var app = new Application();
            app.LoadAssemblies(path);

            var apiEntryPoints = app.GetEntryPoints("Monex.*Controller$", "FXDB.*Controller$", "FXDB2.Services.*Service$");

            Func<TypeReference, bool> filter = x => x.Namespace.StartsWith("Monex") || x.Namespace.Contains("FXDB");

            var xx = app.InheritanceMap.OrderByDescending(x => x.Value.Count)
                .Where(x => filter(x.Key))
                        .Take(10).ToList();

            //Func<TypeReference, bool> filter = x => x.Namespace.Contains("RZD");
            //Func<TypeReference, bool> filter = x => x.Namespace.Contains("Scenario02");
            //Func<TypeReference, bool> filter = null; 
            //Func<TypeReference, bool> filter = x => !x.Namespace.StartsWith("System");

            var repoInterfaces = app.AllTypes.InterfacesOnly().ToList();
            var allTypes = app.AllTypes
                .Filtered(".*$")
                .Where(filter)
                .Resolve()
                .ToList();
            var allTypesDef = allTypes.Resolve();

            Func<TaggedEdge<string, List<Relation>>, TaggedEdge<string, List<Relation>>, TaggedEdge<string, List<Relation>>>
                 edgeMergeFunc = (e1, e2) => new TaggedEdge<string, List<Relation>>(e1.Source, e2.Target, e1.Tag.Concat(e2.Tag).ToList());

            var g1 = app.BuildDependencyGraph(apiEntryPoints, filter);
            var g1s = g1.SerializeToGraphviz();

            //g1.DeleteVerticesAndMergeEdges(x => x.EndsWith("Dto"), edgeMergeFunc);
            //g1.DeleteVerticesAndMergeEdges(x => x.Contains("Monex.Dto"), edgeMergeFunc);
            //g1.DeleteVerticesAndMergeEdges(x => x.Contains("Monex.Data"), edgeMergeFunc);
            //g1.DeleteVerticesAndMergeEdges(x => x.Contains("Monex.Core"), edgeMergeFunc);
            //var g1s1 = g1.SerializeToGraphviz();


            var incorrect = g1.Edges.Where(x => x.Source.EndsWith("IUnitOfWork") && x.Target.EndsWith("MembershipUnitOfWork")).ToList();

            incorrect.ForEach(x => g1.RemoveEdge(x));


            // var g2 = allTypes.BuildDependencyGraph();
            var g3 = new BidirectionalGraph<string, TaggedEdge<string, List<Relation>>>(false);


            var dbContexts = app.AllTypes.Filtered("DbContext$").ToList();
            var targets = app.AllTypes.Where(x => x.Name.Contains("OrbisXmlDataParser") || x.Name.Contains("Twilio")).ToList();

            var paths1 = g1.GetAllPaths(apiEntryPoints.Select(x => x.FullName), targets.Select(x => x.FullName)).ToList();

            var gs2 = g1.BuildMinimumSpanningTree(apiEntryPoints.Select(x => x.FullName), targets.Select(x => x.FullName));
            var gs2s = gs2.SerializeToGraphviz();

            var allNodes = paths1.SelectMany(x => x.SelectMany(y => new[] { y.Source, y.Target })).Distinct().ToList();

            var entryPointsConnected = paths1.Select(x => x.First().Source).Distinct().Ordered()
                .ToList();

            var xxx = string.Join("\n", entryPointsConnected);

            // var paths2 = g2.GetAllPaths(apiEntryPoints.Select(x => x.FullName), targets.Select(x => x.FullName)).ToList();
            var verticiesToRemove = g1.Vertices.Except(allNodes).ToList();
            g1.DeleteVerticesAndMergeEdges(verticiesToRemove, 
                (e1, e2) => new TaggedEdge<string, List<Relation>>(e1.Source, e2.Target, null));

            var gs3s = g1.SerializeToGraphviz();

            //g1.CutNonReachable(entries.Select(x => x.FullName));


            var g2s = g3.SerializeToGraphviz();

            var allPaths = new List<IEnumerable<TypeReference>>();
            var allPathStrings = new List<string>();

            var sourceTypes = allTypesDef.Filtered(".*Service$")
                .Union(allTypesDef.Filtered(".*Controller"))
                .ClassesOnly()
                .Select(x => x.FullName)
                .ToList();

            var targetTypes1 = allTypesDef.Filtered(".*Program$")
                .Union(allTypesDef.Filtered(".*Context$"))
                .Union(allTypesDef.Filtered(".*Global.*$"))
                .Union(allTypesDef.Filtered(".*Repository.*$"))
                .Union(allTypesDef.Filtered(".*FxdbModel.*$"))
                .Select(x => x.FullName)
                .ToList();


            var targetTypes = allTypesDef
                //.Filtered("F.*Context$")
                .IncludingNested()
                .Filtered("Membership$")
                .ClassesOnly()
                .Select(x => x.FullName)
                .ToHashSet();


            var gs1 = g1.BuildMinimumSpanningTree(sourceTypes, targetTypes);

            foreach (var item in allPaths)
            {
                var pathString = string.Join(" -> ", item.Select(x => x.GetShortName()));
                allPathStrings.Add(pathString);
            }


            //var dbContextTypes = app.GetTypes("FxdbContext$").ToList();
            //var repoTypes = app.GetTypes("Repository$").ToList();
        }

    }
}