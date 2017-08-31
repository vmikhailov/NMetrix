using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NMetrics
{
    public class TypeDefinitionEqualityComparer : IEqualityComparer<TypeDefinition>
    {
        public bool Equals(TypeDefinition x, TypeDefinition y)
        {
            if (x == null || y == null)
            {
                return false;
            }
            return x.FullName.Equals(y.FullName);
        }

        public int GetHashCode(TypeDefinition obj)
        {
            return obj?.FullName.GetHashCode() ?? 0;
        }
    }

    public class TypeDefinitionComparer : IComparer<TypeDefinition>
    {
        public int Compare(TypeDefinition x, TypeDefinition y)
        {
            return CultureInfo.InvariantCulture.CompareInfo.Compare(x?.FullName, y?.FullName);
        }
    }

    internal class TypeCacheInfo
    {
        public ISet<TypeDefinition> UsedTypes { get; set; }
        public bool Processing { get; set; }
    }

    public static class MonoCecilExtensions
    {
        public static readonly Dictionary<TypeDefinition, ISet<TypeDefinition>> usageCache =
            new Dictionary<TypeDefinition, ISet<TypeDefinition>>();

        public static readonly Dictionary<TypeDefinition, ISet<TypeDefinition>> directUsageCache =
            new Dictionary<TypeDefinition, ISet<TypeDefinition>>();


        public static IEnumerable<TypeDefinition> GetTypes(this AssemblyDefinition assembly)
        {
            return assembly.MainModule.Types;
        }

        public static IEnumerable<TypeDefinition> GetTypes(this IEnumerable<AssemblyDefinition> assemblies)
        {
            return assemblies.SelectMany(assembly => assembly.GetTypes());
        }

        public static IEnumerable<TypeDefinition> GetTypes(this IEnumerable<AssemblyDefinition> assemblies,
            string regexPattern)
        {
            var regex = new Regex(regexPattern);
            return assemblies.GetTypes().Where(x => regex.IsMatch(x.FullName));
        }

        public static IEnumerable<TypeDefinition> InterfacesOnly(this IEnumerable<TypeDefinition> types)
        {
            return types.Where(x => x.IsInterface);
        }

        public static IEnumerable<TypeDefinition> ClassesOnly(this IEnumerable<TypeDefinition> types)
        {
            return types.Where(x => !x.IsInterface);
        }


        private static TypeDefinition Resolve(TypeReference reference)
        {
            try
            {
                if (reference.Module.Assembly.FullName.StartsWith("DocumentFormat"))
                {
                    return null;
                }
                return reference.Resolve();
            }
            catch(Exception ex)
            {
                return null;
            }
        }

        private static MethodDefinition Resolve(MethodReference reference)
        {
            try
            {
                return reference.Resolve();
            }
            catch(Exception ex)
            {
                if (reference.Module.Assembly.FullName.StartsWith("DocumentFormat"))
                {
                    return null;
                }
                return null;
            }
        }

        private static IEnumerable<TypeDefinition> GetFullHierachy(TypeDefinition type)
        {
            yield return type;
            var t = type.BaseType;
            while (t != null && t.FullName != typeof(object).FullName)
            {
                var typeDef = Resolve(t);
                if (typeDef != null)
                {
                    yield return typeDef;
                    t = typeDef.BaseType;
                }
                else
                {
                    break;
                }
            }
        }

        public static IEnumerable<TypeDefinition> WithAttribute(this IEnumerable<TypeDefinition> types,
            string attributePattern)
        {
            var regex = new Regex(attributePattern);
            return types.Where(x => x.CustomAttributes.Any(y => regex.IsMatch(y.AttributeType.Name)));
        }

        public static IEnumerable<TypeDefinition> InheritedFrom(this IEnumerable<TypeDefinition> types,
            IEnumerable<TypeDefinition> baseTypes)
        {
            var baseTypesList = baseTypes.ToList();
            return types.Where(x => GetFullHierachy(x).Intersect(baseTypesList).Any());
        }

        public static ISet<T> ToHashSet<T>(this IEnumerable<T> collection, IEqualityComparer<T> comparer = null)
        {
            return new HashSet<T>(collection, comparer);
        }

        public static ISet<T> ToSortedSet<T>(this IEnumerable<T> collection, IComparer<T> comparer = null)
        {
            return new SortedSet<T>(collection, comparer);
        }

        public static ISet<TypeDefinition> ToHashSet(this IEnumerable<TypeDefinition> colletion)
        {
            return colletion.ToHashSet(new TypeDefinitionEqualityComparer());
        }

        public static ISet<TypeDefinition> ToHashSet(this IEnumerable<ISet<TypeDefinition>> sets)
        {
            var union = new HashSet<TypeDefinition>(new TypeDefinitionEqualityComparer());
            foreach (var st in sets)
            {
                union.UnionWith(st);
            }
            return union;
        }

        public static ISet<TypeDefinition> ToSortedSet(this IEnumerable<TypeDefinition> collection)
        {
            return collection.ToSortedSet(new TypeDefinitionComparer());
        }

        public static IEnumerable<TypeDefinition> WithInterface(
            this IEnumerable<TypeDefinition> types,
            IEnumerable<TypeDefinition> interfacesToCheck)
        {
            var materializedInterfaces = interfacesToCheck.ToHashSet(new TypeDefinitionEqualityComparer());

            return types.Where(x =>
                                   x.HasInterfaces &&
                                   materializedInterfaces.Overlaps(x.Interfaces.Select(y => y.Resolve())));
        }

        public static IEnumerable<TypeDefinition> WithInterface(
            this IEnumerable<TypeDefinition> types,
            TypeDefinition interfaceToCheck)
        {
            return types.WithInterface(new[] { interfaceToCheck });
        }

        public static IEnumerable<TypeDefinition> WithInterface(
            this IEnumerable<TypeDefinition> types,
            string interfaceName, IEnumerable<AssemblyDefinition> assemblies)
        {
            return types.WithInterface(assemblies.GetTypes(interfaceName).InterfacesOnly());
        }

        public static bool IsAnyOfTypesUsed(ISet<TypeDefinition> typesToFind, TypeReference type)
        {
            if (type == null)
            {
                return false;
            }
            if (type.IsGenericInstance)
            {
                var generic = (GenericInstanceType)type;
                var parameters = generic.GenericArguments;
                return IsAnyOfTypesUsed(typesToFind, parameters);
            }
            var y = typesToFind.Select(x => x.FullName).Contains(type.FullName);
            if (y)
            {
            }
            return y;
        }

        public static IEnumerable<TypeDefinition> GetAllUsedTypes(this TypeDefinition type,
            ISet<string> nmspc)
        {
            return new[] { type }.AllUsedTypes(nmspc);
        }

        public static IEnumerable<TypeDefinition> AllUsedTypes(this IEnumerable<TypeDefinition> types,
            ISet<string> nmspc)
        {
            var processing = new HashSet<TypeDefinition>(new TypeDefinitionEqualityComparer());
            var usedTypes = types.GetAllUsedTypes(processing);

            foreach (var nm in nmspc)
            {
                
            }
            return usedTypes;
        }

        private static ISet<TypeDefinition> GetAllUsedTypes(this IEnumerable<TypeDefinition> types, ISet<TypeDefinition> processing)
        {
            return types.Select(x => x.GetAllUsedTypes(processing)).ToHashSet();
        }

        private static ISet<TypeDefinition> GetAllUsedTypes(this TypeDefinition type, ISet<TypeDefinition> processing)
        {
            ISet<TypeDefinition> usage;
            if (!usageCache.TryGetValue(type, out usage))
            {
                usageCache[type] = type.GetAllUsedTypesImpl(processing);
            }

            return usage;
        }

        private static ISet<TypeDefinition> GetAllUsedTypesImpl(this TypeDefinition type, ISet<TypeDefinition> processing)
        {
            if (!processing.Add(type))
            {
                return processing;
            }

            var directlyUsed = type.DirectlyUsed();
            var toProcess = directlyUsed.Except(processing);
            var usage = toProcess.GetAllUsedTypes(processing);
            return usage;
        }


        public static int CacheHit;
        public static int TotalHit;

        public static ISet<TypeDefinition> DirectlyUsed(this TypeDefinition type)
        {
            ISet<TypeDefinition> usage;
            if (!directUsageCache.TryGetValue(type, out usage))
            {
                directUsageCache[type] = usage = type.GetDirectlyUsed();
            }
            return usage;
        }

        private static ISet<TypeDefinition> GetDirectlyUsed(this TypeDefinition typeToAnalyze)
        {
            var methodsToInspect =
            (from m in typeToAnalyze.Methods
             where m.HasBody
             select new { method = m, intructions = m.Body.Instructions }).ToList();

            var usageByTypeRef =
                from m in methodsToInspect
                from c in m.intructions
                where c.Operand is TypeReference
                let operand = c.Operand as TypeReference
                select new UsageInfo
                (
                    usedType: Resolve(operand),
                    usedMethod: null,
                    usingType: m.method.DeclaringType,
                    usingMethod: m.method,
                    op: c,
                    offset: c.Offset,
                    kind: UsageKind.TypeReference
                );

            var usageByMethodCall =
                from m in methodsToInspect
                from c in m.intructions
                where c.Operand is MethodReference
                let operand = c.Operand as MethodReference
                let resolvedMethod = Resolve(operand)
                select new UsageInfo
                (
                    usedType: Resolve(operand.DeclaringType),
                    usedMethod: Resolve(operand.GetElementMethod()),
                    usingType: m.method.DeclaringType,
                    usingMethod: m.method,
                    op: c,
                    offset: c.Offset,
                    kind: resolvedMethod != null
                        ? (resolvedMethod.IsGetter
                            ? UsageKind.ImmutableAccess
                            : (resolvedMethod.IsConstructor ? UsageKind.Construction : UsageKind.MutableAccess))
                        : UsageKind.Unknown
                    //| (c.OpCode == OpCodes.Newobj ? UsageKind.Explicit : UsageKind.Implicit)
                );

            var l1 = usageByMethodCall;
            var l2 = usageByTypeRef;
            var types = l1.Concat(l2)
                          .Where(x => x.UsedType != null)
                          .Select(x => x.UsedType)
                          //.Where(x => nmspc.Any(y => x.FullName.StartsWith(y)))
                          .ToHashSet();
            return types;
        }

     

        public static bool IsAnyOfTypesUsed(ISet<TypeDefinition> typesToFind, IEnumerable<TypeReference> typesToAnalyze)
        {
            foreach (var type in typesToAnalyze)
            {
                IsAnyOfTypesUsed(typesToFind, type);
            }
            return false;
        }

        public static IEnumerable<UsageInfo> UsingType(
            this IEnumerable<TypeDefinition> types,
            TypeDefinition typeToFind)
        {
            return UsingType(types, new[] { typeToFind });
        }

        public static IEnumerable<UsageInfo> UsingType(
            this IEnumerable<TypeDefinition> types,
            IEnumerable<TypeDefinition> typesToFind)
        {
            var typesToFindSet = typesToFind.ToHashSet();
            //find all base classes
            //var changed = true;
            //while (changed)
            //{
            //    changed = false;
            //    var baseTypes = new HashSet<TypeDefinition>();
            //    var typesToAdd = typesToFindSet.SelectMany(GetFullHierachy).ToList();
            //    foreach (var t in typesToAdd)
            //    {
            //        changed |= typesToFindSet.Add(t);
            //    }
            //}

            var methodsToInspect =
            (from t in types
             from m in t.Methods
             where m.HasBody
             //&& m.Name == "CreateCurrencyPair"
             select new { method = m, intructions = m.Body.Instructions }).ToList();

            var usageByTypeRef =
                from m in methodsToInspect
                from c in m.intructions
                where c.Operand is TypeReference
                let operand = c.Operand as TypeReference
                where IsAnyOfTypesUsed(typesToFindSet, operand)
                select new UsageInfo
                (
                    usedType: operand.Resolve(),
                    usedMethod: null,
                    usingType: m.method.DeclaringType,
                    usingMethod: m.method,
                    op: c,
                    offset: c.Offset,
                    kind: UsageKind.TypeReference
                );

            var usageByMethodCall =
                from m in methodsToInspect
                from c in m.intructions
                where c.Operand is MethodReference
                let operand = c.Operand as MethodReference
                where IsAnyOfTypesUsed(typesToFindSet, operand.DeclaringType)
                let resolvedMethod = operand.Resolve()
                select new UsageInfo
                (
                    usedType: operand.DeclaringType.Resolve(),
                    usedMethod: operand.GetElementMethod().Resolve(),
                    usingType: m.method.DeclaringType,
                    usingMethod: m.method,
                    op: c,
                    offset: c.Offset,
                    kind: (resolvedMethod.IsGetter
                        ? UsageKind.ImmutableAccess
                        : (resolvedMethod.IsConstructor ? UsageKind.Construction : UsageKind.MutableAccess))
                    //| (c.OpCode == OpCodes.Newobj ? UsageKind.Explicit : UsageKind.Implicit)
                );

            var l1 = usageByMethodCall.ToList();
            var l2 = usageByTypeRef.ToList();
            var usageOfBaQuery = l1.Union(l2);
            return usageOfBaQuery.Distinct().ToList();
        }

        public static IEnumerable<UsageInfo> AllUsages(this TypeDefinition type)
        {
            return null;
        }

        public static IEnumerable<UsageInfo> ExtendUsageInScope(this IEnumerable<UsageInfo> usage,
            IEnumerable<TypeDefinition> scope)
        {
            var materializedUsage = usage.ToList();
            var types = materializedUsage.Select(x => x.UsingType).Distinct();
            var newUsings = scope.UsingType(types);
            var diff = newUsings.Except(materializedUsage).ToList();
            return diff;
        }


        public static IEnumerable<T> FillIteratively<T>(this IEnumerable<T> init, Func<T, IEnumerable<T>> func)
        {
            IEnumerable<T> all = init.ToList();
            var current = all;
            while (current.Any())
            {
                IEnumerable<T> result = new T[0];
                result = current.Aggregate(result, (x, y) => x.Union(func(y)));
                result = result.Except(all).ToList();
                foreach (var r in result)
                {
                    yield return r;
                }
                current = result;
            }
        }
    }
}