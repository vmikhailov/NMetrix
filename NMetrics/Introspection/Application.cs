using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NMetrics.Relations;
using QuickGraph;
using MoreLinq;

namespace NMetrics.Introspection
{
    public class Application
    {
        private const string defaultMask = "*.exe;*.dll";
        private readonly IEqualityComparer<TypeReference> comparer;

        public Application()
        {
            comparer = new TypeReferenceEqualityComparer();
            Assemblies = new HashSet<AssemblyDefinition>(new AssemblyDefinitionEqualityComparer());
            AllTypes = new HashSet<TypeDefinition>(comparer);
            InheritanceMap = new Dictionary<TypeReference, ISet<TypeDefinition>>(comparer);
            InterfaceMap = new Dictionary<TypeReference, ISet<TypeDefinition>>(comparer);
        }


        #region Public Properties

        public ISet<AssemblyDefinition> Assemblies { get; }
        public ISet<TypeDefinition> AllTypes { get; }
        public IDictionary<TypeReference, ISet<TypeDefinition>> InheritanceMap { get; }
        public IDictionary<TypeReference, ISet<TypeDefinition>> InterfaceMap { get; }

        #endregion

        #region Public Methods

        public IEnumerable<TypeDefinition> GetTypesInheritedFrom(IEnumerable<TypeDefinition> baseTypes)
        {
            Func<TypeDefinition, IEnumerable<TypeDefinition>> nextLevelFunc =
                t => InheritanceMap.GetValue(t) ?? new HashSet<TypeDefinition>(comparer);

            return baseTypes.Traverse(nextLevelFunc, true, comparer);
        }

        public IEnumerable<TypeDefinition> GetEntryPoints(params string[] filters)
        {
            var allMatches = filters.SelectMany(AllTypes.Filtered).ToHashSet(comparer);
            var classes = allMatches.Where(x => !x.IsInterface).ToList();
            var interfaces = allMatches.Where(x => x.IsInterface).ToList();

            var allTypesToProcess = interfaces
                .Traverse(t => InterfaceMap.GetValue(t) ?? new HashSet<TypeDefinition>(comparer), true, comparer)
                .Where(x => x.IsDefinition && !x.Resolve().IsInterface)
                .Select(x => x.Resolve())
                .Union(classes)
                .ToList();

            return GetTypesInheritedFrom(allTypesToProcess).Where(x => !x.IsAbstract).ToList();
        }

        //private Func<TK, TV> GetTypeMapIterator<TK, TV>(IDictionary<TK, TV> dict)
        //{
        //    Func<TK, TV> nextLevelFunc = t => dict.GetValue(t) ?? new HashSet<TV>(comparer);

        //    return nextLevelFunc;
        //}

        #endregion

        #region Graph Processing 

        public IMutableBidirectionalGraph<string, TaggedEdge<string, List<Relation>>> BuildDependencyGraph
            (IEnumerable<TypeReference> entryPoints, Func<TypeReference, bool> typeFilter = null)
        {
            var entryPointsList = entryPoints.ToList();
            typeFilter = typeFilter ?? (t => !t.Namespace.StartsWith("System"));

            var typesUsages = Usages(entryPointsList, typeFilter).ToList();

            var distinctTypesRelations = typesUsages.Where(x => x.Kind != RelationKind.Interface).Compact().ToList();

            var graph = new BidirectionalGraph<string, TaggedEdge<string, List<Relation>>>(false);
            graph.AddVertexRange(entryPointsList.Select(x => x.FullName));
            graph.AddVerticesAndEdgeRange(
                distinctTypesRelations.Select(
                    x => new TaggedEdge<string, List<Relation>>(x.Source, x.Target, x.Usage)));

            return graph;
        }

        public IEnumerable<Relation> Usages(IEnumerable<TypeReference> entryPoints, Func<TypeReference, bool> userTypeFilter = null, Func<MethodDefinition, bool> methodFilter = null)
        {
            Func<TypeReference, bool> systemTypeFilter = t => !t.Namespace.StartsWith("System") &&
                                                              !t.Namespace.StartsWith("Microsoft");

            var typeFilter = userTypeFilter == null
                ? systemTypeFilter
                : (t => userTypeFilter(t) && systemTypeFilter(t));

            methodFilter = methodFilter ?? (m => true);
            var result = new List<IEnumerable<Relation>>();
            var result0 = new List<Relation>();
            result.Add(result0);

            ISet<TypeReference> processedTypes = null;
            var currentLevel = entryPoints.ToMembersSet();
            Console.WriteLine($"Usage walkthrough:");

            while (currentLevel.Any())
            {
                Console.WriteLine($"{currentLevel.Count()} {result.Sum(x => x.Count())}");

                var nextLevel = new List<IEnumerable<TypeReference>>();
                var nextLevel0 = new List<TypeReference>();
                nextLevel.Add(nextLevel0);

                var dups = currentLevel.GroupBy(x => x.FullName)
                                       .Where(x => x.Count() > 1)
                                       .OrderByDescending(x => x.Count())
                                       .FirstOrDefault()
                                       ?
                                       .ToList();

                foreach (var tp in currentLevel)
                {
                    if (tp.IsGenericParameter)
                    {
                        var gp = (GenericParameter)tp;
                        //emit usage of type as generic parameter contraints
                        if (gp.Constraints.Any())
                        {
                            nextLevel.Add(gp.Constraints);
                            result.Add(gp.Constraints.Select(x => Relation.GenericContraint(tp, x)));
                        }
                        continue;
                    }

                    if (tp.IsGenericInstance)
                    {
                        var gi = (GenericInstanceType)tp;
                        nextLevel0.Add(gi.GetElementType());
                        //emit usage of type as generic instance
                        result0.Add(Relation.GenericDefinition(tp, gi.GetElementType()));

                        //emit usage of types as generic instance arguments
                        nextLevel.Add(gi.GenericArguments);
                        result.Add(gi.GenericArguments.Select(x => Relation.GenericParameter(tp, x)));
                        continue;
                    }

                    if (tp.IsArray || tp.IsByReference)
                    {
                        nextLevel0.Add(tp.GetElementType());
                        //emit usage of type as generic parameter
                        continue;
                    }

                    if (tp.IsFunctionPointer || tp.IsPointer)
                    {
                        //too complicated;
                        continue;
                    }

                    var resolvedType = tp.SmartResolve();
                    var usage = UsagesForConreteType(resolvedType, typeFilter, methodFilter).ToList();
                    nextLevel.Add(usage.Select(x => x.Target)); //.ToHashSet());
                    result.Add(usage);
                }
                //next iteration
                if (processedTypes == null)
                {
                    processedTypes = currentLevel;
                }
                else
                {
                    processedTypes.UnionWith(currentLevel);
                }

                //set of unique items
                currentLevel = nextLevel
                    .SelectMany(x => x)
                    .Where(typeFilter)
                    .OrderBy(x => x.FullName)
                    .ToMembersSet();

                //excepting already processed
                currentLevel.ExceptWith(processedTypes);
            }
            return result.SelectMany(x => x);
        }


        private IEnumerable<Relation> UsagesForConreteType(
            TypeDefinition type,
            Func<TypeReference, bool> typeFilter,
            Func<MethodDefinition, bool> methodFilter)
        {
            //resolve type to get interfaces, methods and code
            //var typeDef = type.SmartResolve();
            Func<TypeReference, bool> compilerGenerated =
                x => x.FullName.StartsWith("<>") ||
                     (x.SmartResolve()?.HasCustomAttribute<CompilerGeneratedAttribute>() ?? false);

            var baseClass = type.BaseType != null
                ? Relation.InheritedFrom(type, type.BaseType).AsEnumerableWithOneItem()
                : Enumerable.Empty<Relation>();
            var nested = type.NestedTypes.Select(x => Relation.ContainsNested(type, x));

            var interfaceImpls = InterfaceMap.GetValue(type, Enumerable.Empty<TypeDefinition>()).Select(x => Relation.ImplementedBy(type, x)).ToList();
            var interfaces = type.Interfaces.Select(x => Relation.Implements(type, x)).ToList();

            var methodsToInspect = type.Methods.Where(x => x.HasBody && methodFilter(x)).ToList();

            var usageAsMethodParameter =
                from m in methodsToInspect
                from p in m.Parameters
                select Relation.AsMethodParameter(type, m, p.ParameterType);

            var usageAsReturnType =
                from m in methodsToInspect
                select Relation.AsMethodReturnType(type, m, m.ReturnType);

            var usageInCode =
                from m in methodsToInspect
                from i in m.Body.Instructions
                where i.Operand != null
                let u = GetUsagesInConreteInstruction(m, i)
                where u != null
                select u;

            var allUsages = baseClass
                .Concat(nested)
                //.Concat(generics)
                .Concat(interfaces)
                .Concat(interfaceImpls)
                .Concat(usageAsMethodParameter)
                .Concat(usageAsReturnType)
                .Concat(usageInCode)
                .Where(x => typeFilter(x.Target));
            //.Where(x => !x.Target.IsPrimitive & !x.Target.Namespace.StartsWith("System"))
            //.Where(x => !compilerGenerated(x.Target));
            //var generics = type.GetGenericArguments().Select(x => UsageInfo.AsGenericParameter(typeDef, x)).ToList();
            return allUsages;
        }

        private static Relation GetUsagesInConreteInstruction(MethodDefinition method, Instruction instruction)
        {
            var op = instruction.Operand;
            //direct mention that types in all method's instructions 
            var typeRef = op as TypeReference;
            if (typeRef != null)
            {
                return Relation.TypeReference(method.DeclaringType, method, typeRef)
                               .WithInstruction(instruction);
            }

            var methodRef = op as MethodReference;
            if (methodRef != null)
            {
                return Relation.MethodCall(method.DeclaringType, method, methodRef.DeclaringType, methodRef)
                               .WithInstruction(instruction);
            }

            if (op is FieldDefinition || op is ParameterDefinition || op is Instruction || op is VariableDefinition)
            {
                //just skip it for now. 
                return null;
            }

            //unknown op
            return null;
        }

        #endregion

        #region Assembly Loading

        public IEnumerable<AssemblyDefinition> LoadAssemblies(string path, string mask = defaultMask)
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(path);

            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadingMode = ReadingMode.Deferred
            };

            var newAssemblies = new List<AssemblyDefinition>();
            foreach (var file in ScanDirectory(path, mask))
            {
                var asm = LoadAssemblyImpl(file, readerParameters);
                if (asm != null && Assemblies.Add(asm))
                {
                    newAssemblies.Add(asm);
                }
            }

            RefreshTypes(newAssemblies);
            return newAssemblies;
        }

        //private AssemblyDefinition LoadAssembly(string path)
        //{
        //    var asm = LoadAssemblyImpl(path);
        //    RefreshTypes(asm);
        //    return asm;
        //}

        private AssemblyDefinition LoadAssemblyImpl(string path, ReaderParameters readerParameters)
        {
            try
            {
                return AssemblyDefinition.ReadAssembly(path, readerParameters);
            }
            catch
            {
                //assembly may not load for various reasons.
                return null;
            }
        }

        private static IEnumerable<string> ScanDirectory(string path, string mask)
        {
            var masks = mask?.Split(';') ?? new[] { string.Empty };
            if (!new DirectoryInfo(path).Exists)
            {
                yield break;
            }

            foreach (var singleMask in masks)
            {
                var files = Directory.EnumerateFiles(path, singleMask, SearchOption.AllDirectories);

                foreach (var fname in files)
                {
                    yield return fname;
                }
            }
        }

        #endregion

        #region Types Introspection

        public void RefreshTypes()
        {
            AllTypes.Clear();
            InheritanceMap.Clear();
            InterfaceMap.Clear();
            RefreshTypes(Assemblies);
        }

        public void RefreshTypes(AssemblyDefinition assembly)
        {
            if (assembly != null)
            {
                RefreshTypes(assembly.AsEnumerableWithOneItem());
            }
        }

        private void RefreshTypes(IEnumerable<AssemblyDefinition> assemblies)
        {
            var types = assemblies.Where(a => a != null)
                                  .SelectMany(a => a.Modules.SelectMany(m => m.Types))
                                  .Where(x => !x.HasCustomAttribute<CompilerGeneratedAttribute>())
                                  .ToList();

            AllTypes.UnionWith(types);
            RefreshInheritanceMap(types);
            RefreshInterfaceMap(types);
        }

        private void RefreshInheritanceMap(IEnumerable<TypeDefinition> types)
        {
            var mapDelta = AllTypes.Where(x => x.BaseType != null).ToLookup(x => x.BaseType, comparer);

            foreach (var item in mapDelta)
            {
                var derivedTypes = InheritanceMap.GetValue(item.Key) ?? new HashSet<TypeDefinition>(comparer);
                derivedTypes.UnionWith(item);
                InheritanceMap[item.Key] = derivedTypes;
            }
        }

        private void RefreshInterfaceMap(IEnumerable<TypeDefinition> types)
        {
            var mapDelta = AllTypes.Where(x => x.Interfaces.Any())
                                   .SelectMany(x => x.Interfaces.Select(y => new { i = y, t = x }))
                                   .ToLookup(x => x.i, x => x.t, comparer);

            foreach (var item in mapDelta)
            {
                var implementations = InterfaceMap.GetValue(item.Key) ?? new HashSet<TypeDefinition>(comparer);
                implementations.UnionWith(item);
                InterfaceMap[item.Key] = implementations;
            }
        }

        #endregion
    }
}