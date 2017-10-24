using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NMetrics.Relations;
using QuickGraph;
using QuickGraph.Algorithms;
using QuickGraph.Graphviz;
using MoreLinq;

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
            return assembly.Modules.SelectMany(x => x.Types);
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

        public static MethodDefinition SmartResolve(this MethodReference reference)
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

        public static ISet<T> ToHashSet<T>(this IEnumerable<T> collection)
        {
            return new HashSet<T>(collection);
        }


        public static ISet<T> ToHashSet<T, TV>(this IEnumerable<T> collection, IEqualityComparer<TV> comparer = null)
            where T : TV
        {
            return new HashSet<T>(collection, comparer as IEqualityComparer<T>);
        }

        public static ISet<T> ToMembersSet<T>(this IEnumerable<T> collection)
            where T : TypeReference
        {
            return collection.ToHashSet(new TypeReferenceEqualityComparer());
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

        // public static IShallowTypesGraph Usages(

        public static bool HasCustomAttribute<T>(this TypeDefinition type)
        {
            return type.HasCustomAttributes && type.CustomAttributes.Any(x => x.AttributeType.FullName == typeof(T).FullName);
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
}