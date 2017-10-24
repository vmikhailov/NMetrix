using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using NMetrics.Relations;
using QuickGraph;
using QuickGraph.Algorithms;
using QuickGraph.Graphviz;
using MoreLinq;

namespace NMetrics
{
    public static class QuickGraphExtensions
    {
        public static ISet<TVertex> FindReachable<TVertex, TEdge>(this IVertexAndEdgeListGraph<TVertex, TEdge> graph,
            IEnumerable<TVertex> source,
            IEqualityComparer<TVertex> comparer = null)
            where TEdge : IEdge<TVertex>
        {
            var availableSources = source.Intersect(graph.Vertices, comparer);
            var processed = availableSources
                .Traverse(x => graph.OutEdges(x).Select(y => y.Target))
                .ToHashSet(comparer);
            return processed;
        }

        public static void BuildConnectedOutSubGraph<TVertex, TEdge>(
            this IVertexAndEdgeListGraph<TVertex, TEdge> graph,
            IMutableVertexAndEdgeListGraph<TVertex, TEdge> subGraph,
            IEnumerable<TVertex> entries,
            Func<TVertex, TVertex, TEdge> edgeFactory)
            where TEdge : IEdge<TVertex>
        {
            var reachable = graph.FindReachable(entries);
            var neededEdges = graph.GetAllEdges(reachable).ToList();
            subGraph.AddVerticesAndEdgeRange(neededEdges);
        }

        //public static void BuildConnectedSubGraph<TVertex, TEdge>(
        //    this IVertexAndEdgeListGraph<TVertex, TEdge> graph,
        //    IMutableVertexAndEdgeListGraph<TVertex, TEdge> subGraph,
        //    IEnumerable<TVertex> entries,
        //    Func<TVertex, TVertex, TEdge> edgeFactory)
        //    where TEdge : IEdge<TVertex>
        //{
        //    var verticiesToKeep = graph.Vertices.Intersect(entries).ToList();
        //    var verticiesToRemove = graph.Vertices.Except(verticiesToKeep).ToList();
        //    DeleteVerticesAndMergeEdges(verticiesToRemove, 
        //    foreach (var v in verticiesToRemove)
        //    {
         
        //    }
        //}


        public static void MergeVertices<TVertex, TEdge>(this IMutableBidirectionalGraph<TVertex, TEdge> graph,
            Func<TVertex, TVertex> groupByPredicate,
            Func<TEdge, TVertex, TEdge> mergeInFunc,
            Func<TVertex, TEdge, TEdge> mergeOutFunc,
            IEqualityComparer<TVertex> comparer = null)
            where TEdge : IEdge<TVertex>
        {
            var grouppedVertices = graph.Vertices.ToLookup(groupByPredicate, comparer).ToList();

            foreach (var x in grouppedVertices)
            {
                graph.MergeVertices(x, x.Key, mergeInFunc, mergeOutFunc, comparer);
            }
        }

        public static void MergeVertices<TVertex, TEdge>(this IMutableBidirectionalGraph<TVertex, TEdge> graph, 
            IEnumerable<TVertex> verticesToMerge,
            TVertex replacement,
            Func<TEdge, TVertex, TEdge> mergeInFunc,
            Func<TVertex, TEdge, TEdge> mergeOutFunc,         
            IEqualityComparer<TVertex> comparer = null,            
            bool checkExistence = false)
            where TEdge : IEdge<TVertex>
        {
            var verticies = (checkExistence ? graph.Vertices.Intersect(verticesToMerge, comparer) : verticesToMerge).ToList();
            var allInEdges = verticies.SelectMany(graph.InEdges);
            var allOutEdges = verticies.SelectMany(graph.OutEdges);
            var newInEdges = allInEdges.Select(x => mergeInFunc(x, replacement));
            var newOutEdges = allOutEdges.Select(x => mergeOutFunc(replacement, x));

            verticies.ForEach(x => graph.RemoveVertex(x));
            graph.AddVertex(replacement);
            graph.AddEdgeRange(newInEdges);
            graph.AddEdgeRange(newOutEdges);
        }

        public static void DeleteVerticesAndMergeEdges<TVertex, TEdge>(
            this IMutableBidirectionalGraph<TVertex, TEdge> graph,
            Func<TVertex, bool> deletePredicate,
            Func<TEdge, TEdge, TEdge> mergeFunc,
            IEqualityComparer<TVertex> comparer = null)
            where TEdge : IEdge<TVertex>
        {
            var toDelete = graph.Vertices.Where(deletePredicate).ToList();
            foreach (var v in toDelete)
            {
                if (graph.Vertices.Contains(v, comparer))
                {
                    graph.DeleteVerticesAndMergeEdges(v, mergeFunc);
                }
            }
        }

        public static void DeleteVerticesAndMergeEdges<TVertex, TEdge>
        (this IMutableBidirectionalGraph<TVertex, TEdge> graph, IEnumerable<TVertex> vertexToDelete,
            Func<TEdge, TEdge, TEdge> mergeFunc)
            where TEdge : IEdge<TVertex>
        {
            foreach (var v in vertexToDelete)
            {
                graph.DeleteVerticesAndMergeEdges(v, mergeFunc);
            }
        }

        public static void DeleteVerticesAndMergeEdges<TVertex, TEdge>
        (this IMutableBidirectionalGraph<TVertex, TEdge> graph, TVertex vertexToDelete,
            Func<TEdge, TEdge, TEdge> mergeFunc)
            where TEdge : IEdge<TVertex>
        {
            if (graph.IsInEdgesEmpty(vertexToDelete) || graph.IsOutEdgesEmpty(vertexToDelete))
            {
                //simple case
                graph.RemoveVertex(vertexToDelete);
            }
            else
            {
                //need to keep connections
                var newEdges = from e1 in graph.InEdges(vertexToDelete)
                               from e2 in graph.OutEdges(vertexToDelete)
                               select mergeFunc(e1, e2);

                //create edges
                var newEdgesList = newEdges.ToList();

                graph.AddEdgeRange(newEdgesList);
                graph.RemoveVertex(vertexToDelete);
            }
        }


        public static void CutNonReachable<TVertex, TEdge>(this IMutableBidirectionalGraph<TVertex, TEdge> graph,
            IEnumerable<TVertex> toKeep)
            where TEdge : IEdge<TVertex>
        {
            var reachable = graph.FindReachable(toKeep);
            graph.RemoveVertexIf(x => !reachable.Contains(x));
        }

        public static IEnumerable<TEdge> GetAllEdges<TVertex, TEdge>(
            this IVertexAndEdgeListGraph<TVertex, TEdge> graph, ISet<TVertex> vertices)
            where TEdge : IEdge<TVertex>
        {
            return graph.Edges.Where(x => vertices.Contains(x.Source) && vertices.Contains(x.Target));
        }


        public static string SerializeToGraphviz<TVertex, TTag>(
            this IVertexAndEdgeListGraph<TVertex, TaggedEdge<TVertex, TTag>> graph)
        {
            // var graphviz = new GraphvizAlgorithm<string, Edge<string>>(g3);
            var graphviz = new GraphvizAlgorithm<TVertex, TaggedEdge<TVertex, TTag>>(graph);
            graphviz.FormatVertex += delegate (object sender, FormatVertexEventArgs<TVertex> args)
            {
            };

            // render
            return graphviz.Generate();
        }

      
        public static IBidirectionalGraph<TVertex, TaggedEdge<TVertex, TTag>> BuildMinimumSpanningTree<TVertex, TTag>(
            this IBidirectionalGraph<TVertex, TaggedEdge<TVertex, TTag>> graph,
            IEnumerable<TVertex> sources,
            IEnumerable<TVertex> targets,
            Func<TaggedEdge<TVertex, TTag>, double> weightFunc = null)
        {
            var subGraph = new BidirectionalGraph<TVertex, TaggedEdge<TVertex, TTag>>(false);

            foreach (var path in graph.GetAllPaths(sources, targets, weightFunc))
            {
               foreach (var segment in path)
               {
                   var edge = new TaggedEdge<TVertex, TTag>(segment.Source, segment.Target, segment.Tag);
                   subGraph.AddVerticesAndEdge(edge);
               }
            }
            return subGraph;
        }

        public static IEnumerable<List<TaggedEdge<TVertex, TTag>>> GetAllPaths<TVertex, TTag>(
            this IBidirectionalGraph<TVertex, TaggedEdge<TVertex, TTag>> graph,
            IEnumerable<TVertex> sourceTypes,
            IEnumerable<TVertex> targetTypes,
            Func<TaggedEdge<TVertex, TTag>, double> weightFunc = null)
        {
            var sources = sourceTypes.ToHashSet();
            var targets = targetTypes.ToHashSet();
            weightFunc = weightFunc ?? (x => 1);

            foreach (var controller in graph.Vertices.Where(sources.Contains))
            {
                var fromSingleSource = graph.ShortestPathsDijkstra(weightFunc, controller);
                foreach (var repo in graph.Vertices.Where(x => targets.Contains(x)))
                {
                    IEnumerable<TaggedEdge<TVertex, TTag>> pathInGraph;
                    if (fromSingleSource(repo, out pathInGraph))
                    {
                        yield return pathInGraph.ToList();
                    }
                }
            }
        }
    }
}
