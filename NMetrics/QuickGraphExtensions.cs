using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickGraph;

namespace NMetrics
{
    public static class QuickGraphExtensions
    {
        public static void BuildSubGraph<TVertex, TEdge>(
            this IVertexAndEdgeListGraph<TVertex, TEdge> masterGraph,
            IMutableVertexAndEdgeListGraph<TVertex, TEdge> subGraph,
            IEnumerable<TVertex> subsetOfVertecies,
            Func<TVertex, TVertex, TEdge> edgeFactory)
            where TEdge : IEdge<TVertex>
        {
            var subset = subsetOfVertecies.ToHashSet();

            foreach (var v in subsetOfVertecies)
            {
                var toProcess = new HashSet<TVertex>(masterGraph.OutEdgesIfAny(v));
                var expanded = new HashSet<TVertex>();
                while (true)
                {
                    var toExpand = toProcess.Except(subset).Except(expanded).ToHashSet();
                    toProcess.IntersectWith(subset);

                    if (!toExpand.Any()) break;
                    var nextLevel = toExpand.SelectMany(x => masterGraph.OutEdgesIfAny(x)).ToHashSet();
                    expanded.UnionWith(toExpand);
                    toProcess.UnionWith(nextLevel);
                }
                subGraph.AddVertex(v);
                foreach (var t in toProcess.Where(x => !x.Equals(v)))
                {
                    subGraph.AddEdge(edgeFactory(v, t));
                }
            }
        }

        private static IEnumerable<TVertex> OutEdgesIfAny<TVertex, TEdge>(this IVertexAndEdgeListGraph<TVertex, TEdge> graph, TVertex vertex)
             where TEdge : IEdge<TVertex>
        {
            IEnumerable<TEdge> edges;
            if (graph.TryGetOutEdges(vertex, out edges))
            {
                return edges.Select(x => x.Target);
            }
            else
            {
                return Enumerable.Empty<TVertex>();
            }
        }
    }
}
