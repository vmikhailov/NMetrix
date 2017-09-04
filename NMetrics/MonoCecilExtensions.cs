using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using QuickGraph;

namespace NMetrics
{
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
            return assemblies.GetTypes().Filtered(regexPattern);
        }

        public static IEnumerable<TypeDefinition> IncludingNested(this IEnumerable<TypeDefinition> types)
        {
            if (!types.Any())
            {
                return Enumerable.Empty<TypeDefinition>();
            }
            else
            {
                return types.Union(types.Where(x => x.HasNestedTypes).SelectMany(x => x.NestedTypes).IncludingNested());
            }
        }

        public static IEnumerable<TypeDefinition> Filtered(this IEnumerable<TypeDefinition> types,
            string regexPattern)
        {
            var regex = new Regex(regexPattern);
            return types.Where(x => regex.IsMatch(x.FullName));
        }

        public static IEnumerable<TypeDefinition> InterfacesOnly(this IEnumerable<TypeDefinition> types)
        {
            return types.Where(x => x.IsInterface);
        }

        public static IEnumerable<TypeDefinition> ClassesOnly(this IEnumerable<TypeDefinition> types)
        {
            return types.Where(x => !x.IsInterface);
        }

        public static IEnumerable<TypeDefinition> Resolve(this IEnumerable<TypeReference> types)
        {
            return types.Select(x => x.SmartResolve());
        }

        private static readonly IDictionary<MemberReference, MemberReference> cache =
            new Dictionary<MemberReference, MemberReference>();

        private static int count;
        private static int allcount;
        private static ISet<AssemblyDefinition> unresolvedAssemblies = new HashSet<AssemblyDefinition>();

        public static TypeDefinition SmartResolve(this TypeReference reference)
        {
            var td = reference as TypeDefinition;
            if (td != null)
            {
                return td;
            }

            MemberReference mr;
            if (!cache.TryGetValue(reference, out mr))
            {
                cache[reference] = mr = reference.ResolveImpl();
                count++;
            }
            allcount++;
            return (TypeDefinition)mr;
        }

        private static TypeDefinition ResolveImpl(this TypeReference reference)
        {
            try
            {
                if (reference.IsGenericInstance)
                {
                    var previous_instance = (GenericInstanceType)reference;
                    var instance = new GenericInstanceType(previous_instance.ElementType.SmartResolve());
                    foreach (var argument in previous_instance.GenericArguments)
                    {
                        instance.GenericArguments.Add(argument.SmartResolve());
                    }
                    return instance.Resolve();
                }

                return reference.Resolve();
            }
            catch (AssemblyResolutionException ex)
            {
                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private static MethodDefinition SmartResolve(this MethodReference reference)
        {
            MemberReference td;
            if (!cache.TryGetValue(reference, out td))
            {
                cache[reference] = td = ResolveImpl(reference);
                count++;
            }
            allcount++;
            return (MethodDefinition)td;
        }

        private static MethodDefinition ResolveImpl(this MethodReference reference)
        {
            try
            {
                return reference.Resolve();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private static IEnumerable<TypeReference> GetFullHierachy(this TypeReference type)
        {
            while (true)
            {
                yield return type;
                type = type.SmartResolve()?.BaseType;
                if (type == null)
                {
                    yield break;
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
            var dictionaryByBaseType = types
                .Where(x => x.BaseType != null)
                .ToLookup(x => x.BaseType.FullName)
                .ToDictionary(x => x.Key, x => x.ToMembersSet());

            var result = new HashSet<TypeDefinition>(new MemberEqualityComparer<TypeDefinition>());
            var toProcess = baseTypes.ToMembersSet();
            //return types.Where(x => GetFullHierachy(x).Intersect(baseTypesList).Any());
            while (true)
            {
                ISet<TypeDefinition> derivedTypes;
                var nextLevel = new HashSet<TypeDefinition>(new MemberEqualityComparer<TypeDefinition>());
                foreach (var t in toProcess)
                {
                    if (dictionaryByBaseType.TryGetValue(t.FullName, out derivedTypes))
                    {
                        nextLevel.UnionWith(derivedTypes);
                    }
                }
                if (!nextLevel.Any())
                {
                    break;
                }
                result.UnionWith(nextLevel);
                toProcess = nextLevel;
            }
            return result;
        }

        public static ISet<T> ToHashSet<T>(this IEnumerable<T> collection, IEqualityComparer<T> comparer = null)
        {
            return new HashSet<T>(collection, comparer);
        }

        public static ISet<T> ToSortedSet<T>(this IEnumerable<T> collection, IComparer<T> comparer = null)
        {
            return new SortedSet<T>(collection, comparer);
        }

        public static ISet<T> ToMembersSet<T>(this IEnumerable<T> collection)
            where T : MemberReference
        {
            return collection.ToHashSet(new MemberEqualityComparer<T>());
        }

        public static ISet<T> MergeSets<T>(this IEnumerable<ISet<T>> sets)
            where T : MemberReference
        {
            var union = new HashSet<T>(new MemberEqualityComparer<T>());
            foreach (var st in sets)
            {
                union.UnionWith(st);
            }
            return union;
        }

        public static IEnumerable<TypeDefinition> WithInterface(
            this IEnumerable<TypeDefinition> types,
            IEnumerable<TypeDefinition> interfacesToCheck)
        {
            var materializedInterfaces = interfacesToCheck.ToMembersSet();

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

        private static readonly ISet<string> processedTypes = new HashSet<string>();

        // public static IShallowTypesGraph Usages(

        public static IEnumerable<UsageInfo> Usages(
            this IEnumerable<TypeReference> entryPoints,
            Func<TypeReference, bool> typeFilter = null,
            Func<MethodDefinition, bool> methodFilter = null)
        {
            typeFilter = typeFilter ?? (t => !t.Namespace.StartsWith("System"));
            methodFilter = methodFilter ?? (m => true);
            var result = new List<IEnumerable<UsageInfo>>();
            var currentLevel = entryPoints.ToHashSet();
            while (currentLevel.Any())
            {
                var nextLevel = new List<IEnumerable<TypeReference>>();
                var nextLevelSingleTypes = new List<TypeReference>();
                nextLevel.Add(nextLevelSingleTypes);

                foreach (var tp in currentLevel)
                {
                    if (tp.IsGenericParameter)
                    {
                        var gp = tp as GenericParameter;
                        nextLevel.Add(gp.Constraints);
                        //emit usage of type as generic parameter contraints
                        continue;
                    }

                    if (tp.IsGenericInstance)
                    {
                        var gi = tp as GenericInstanceType;
                        nextLevelSingleTypes.Add(gi.GetElementType());
                        //emit usage of type as generic instance

                        nextLevel.Add(gi.GenericArguments);
                        //emit usage of types as generic instance arguments
                        continue;
                    }

                    if (tp.IsArray || tp.IsByReference)
                    {
                        nextLevelSingleTypes.Add(tp.GetElementType());
                        //emit usage of type as generic parameter
                        continue;
                    }

                    if (tp.IsFunctionPointer || tp.IsPointer)
                    {
                        //too complicated;
                        continue;
                    }


                    var resolvedType = tp.SmartResolve();
                    if (resolvedType == null)
                    {
                    }
                    var usage = resolvedType.UsagesForConreteType(typeFilter, methodFilter).ToList();
                    nextLevel.Add(usage.Select(x => x.UsedType));//.ToHashSet());
                    result.Add(usage);
                }
                currentLevel = nextLevel.SelectMany(x => x).Where(typeFilter).ToHashSet();
            }
            return result.SelectMany(x => x);
        }

        //public static IEnumerable<UsageInfo> Usages(
        //    this TypeReference type,
        //    Func<TypeReference, bool> typeFilter = null,
        //    Func<MethodDefinition, bool> methodFilter = null)
        //{

        //    return type
        //        .GetFullHierachy()
        //        .Where(typeFilter)
        //        .SelectMany(t => t.UsagesForConreteType(typeFilter, methodFilter))
        //        ;//.ToList();
        //}

        private static bool HasCustomAttribute<T>(this TypeDefinition type)
        {
            return type.HasCustomAttributes && type.CustomAttributes.Any(x => x.AttributeType.Name == nameof(T));
        }

        public static IEnumerable<UsageInfo> UsagesForConreteType(
            this TypeDefinition type,
            Func<TypeReference, bool> typeFilter,
            Func<MethodDefinition, bool> methodFilter)
        {
            //resolve type to get interfaces, methods and code
            //var typeDef = type.SmartResolve();
            if (processedTypes.Contains(type.FullName))
            {
                return Enumerable.Empty<UsageInfo>();
            }
            processedTypes.Add(type.FullName);

            Func<TypeReference, bool> compilerGenerated =
                x => x.FullName.StartsWith("<>") || (x.SmartResolve()?.HasCustomAttribute<CompilerGeneratedAttribute>() ?? false);

            //generics are available only on typereference
            var baseClass = type.BaseType.AsEnumerableWithOneItem().Select(x => UsageInfo.InheritedFrom(type, x));
            var nested = type.NestedTypes.Select(x => UsageInfo.ContainsNested(type, x));
            // var generics = type.GetGenericArguments().Select(x => UsageInfo.AsGenericParameter(typeDef, x)).ToList();
            var interfaces = type.Interfaces.Select(x => UsageInfo.Implements(type, x)).ToList();
            var methodsToInspect = type.Methods.Where(x => x.HasBody && methodFilter(x)).ToList();

            var usageAsMethodParameter =
                from m in methodsToInspect
                from p in m.Parameters
                select UsageInfo.AsMethodParameter(type, m, p.ParameterType);

            var usageAsReturnType =
                from m in methodsToInspect
                select UsageInfo.AsMethodReturnType(type, m, m.ReturnType);

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
                .Concat(usageAsMethodParameter)
                .Concat(usageAsReturnType)
                .Concat(usageInCode)
                .Where(x => !x.UsedType.IsPrimitive & !x.UsedType.Namespace.StartsWith("System"))
                .Where(x => !compilerGenerated(x.UsedType));

            return allUsages;
        }

        private static UsageInfo GetUsagesInConreteInstruction(MethodDefinition method, Instruction instruction)
        {
            var op = instruction.Operand;
            //direct mention that types in all method's instructions 
            var typeRef = op as TypeReference;
            if (typeRef != null)
            {
                return new UsageInfo
                (
                    usingType: method.DeclaringType,
                    usingMethod: method,
                    usedType: typeRef,
                    usedMethod: null,
                    op: instruction,
                    offset: instruction.Offset,
                    kind: UsageKind.TypeReference
                );
            }

            var methodRef = op as MethodReference;
            if (methodRef != null)
            {
                var resolvedMethod = methodRef.SmartResolve();
                return new UsageInfo
                (
                    usingType: method.DeclaringType,
                    usingMethod: method,
                    usedType: methodRef.DeclaringType,
                    usedMethod: methodRef.GetElementMethod(),
                    op: instruction,
                    offset: instruction.Offset,
                    kind: resolvedMethod != null
                        ? (resolvedMethod.IsGetter
                            ? UsageKind.ImmutableAccess
                            : (resolvedMethod.IsConstructor ? UsageKind.Construction : UsageKind.MutableAccess))
                        : UsageKind.Unknown
                //| (c.OpCode == OpCodes.Newobj ? UsageKind.Explicit : UsageKind.Implicit)
                );
            }

            if (op is FieldDefinition || op is ParameterDefinition || op is Instruction || op is VariableDefinition)
            {
                //just skip it for now. 
                return null;
            }

            //unknown op
            return null;
        }

        public static string GetShortName(this TypeReference type)
        {
            var generic = type as GenericInstanceType;
            if (generic != null)
            {
                var arguments = generic.GenericArguments.Select(x => x.GetShortName());
                var argumentsString = string.Join(", ", arguments);
                var genericName = generic.Name.Split('`').First();
                return $"{genericName}<{argumentsString}>";
            }
            else
            {
                return type.Name;
            }
        }
    }

    public interface IShallowTypesGraph : IBidirectionalGraph<string, TaggedEdge<string, List<UsageInfo>>>
    {
    }
}