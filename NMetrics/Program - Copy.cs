using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace NMetrics
{
    /*
    internal class Program
    {
        private static void Main(string[] args)
        {
            MainMonexPay(args);
        }

        private static void MainPOneUI(string[] args)
        {
            var path = @"C:\Work\LayerOne\CurrentSources\P1TC.UI\POne.UI\bin\Debug\";
            var assemblies = ScanDirectory(path, "*.dll;*.exe");
            var app = assemblies.ToList();

            var allServiceClients = app.GetTypes("Client$")
                                       .Where(x => x.BaseType is TypeDefinition)
                                       .Select(y => (GenericInstanceType)y.BaseType)
                                       .Select(y => y.GenericArguments.First().Resolve())
                                       //.Except(internalServiceClients.Values.SelectMany(x => x))
                                       .ToList();

            foreach (var client in allServiceClients)
            {
                Console.WriteLine($"{client.FullName} used in {client.Scope}");
            }
        }

        private static void MainPOneCore(string[] args)
        {
            var path = @"C:\Work\LayerOne\CurrentSources\P1TC.Core\.Build\Debug";
            var assemblies = ScanDirectory(path, "*.dll;*.exe");
            var app = assemblies.ToList();

            var services = app.GetTypes().WithAttribute("ServiceModule").ToList();

            foreach (var t in app.GetTypes().Where(x => !x.Namespace.StartsWith("System.")).GroupBy(x => x.Namespace))
            {
                foreach (var tt in t)
                {
                    tt.DirectlyUsed();
                }
            }

            var allServiceClients = app.GetTypes("Client$")
                                            .Where(x => x.BaseType?.Name == "ClientBase`1")
                                            .Select(y => (GenericInstanceType)y.BaseType)
                                            .Select(y => y.GenericArguments.First().Resolve())
                                            //.Except(internalServiceClients.Values.SelectMany(x => x))
                                            .ToList();
            var broadcastService = app.GetTypes("BroadcastQuery`1").ToList();
            var dataChangedUsage = app.GetTypes("DataChangeEvent").ToList(); 

            foreach (var service in services)
            {
                var allUsedByService = service.GetAllUsedTypes(new HashSet<string>{ "POne", "Fortress", "P1TC", "LayerOne" }).ToList();
                var broadcastUsage = allUsedByService
                    .Intersect(dataChangedUsage, new TypeDefinitionEqualityComparer())
                    .ToList();

                Console.WriteLine(broadcastUsage.Any()
                                      ? $"{service.FullName} {service.Scope.Name} broadcast"
                                      : $"{service.FullName} {service.Scope.Name} no broadcast");

                var otherUsedServices = allUsedByService.Intersect(allServiceClients, new TypeDefinitionEqualityComparer()).ToList();

                if (otherUsedServices.Any())
                {
                    foreach (var remoteService in otherUsedServices)
                    {
                        //Console.WriteLine($"{service.FullName} {service.Scope.Name} {remoteService.FullName} {remoteService.Scope.Name}");
                    }
                }
                else
                {
                    //Console.WriteLine($"{service.FullName} {service.Scope.Name}");
                }
            }
        }

        private static void MainMonexPay(string[] args)
        {
            var path1 = @"C:\Work\Monex\fxdb2\ossrc\Bin";
            var mask = "*.dll;*.exe";
            //var assemblies = ScanDirectory(path1, mask)
            //    .Union(ScanDirectory(path2, mask))
            //    .DedupFiles();
            var app = ScanDirectory(path1, mask).ToList();

            var repoInterfaces = app.GetTypes().InterfacesOnly().ToList();

            var x = app.GetTypes("Monex.*Ba.*Controller$").ClassesOnly().First().AllUsages();

            var dbContextTypes = app.GetTypes("FxdbContext$").ToList();
            var repoTypes = app.GetTypes("Repository$").ToList();
            var allTypesToSearch = repoTypes.Concat(dbContextTypes);
            var allUsages = app.GetTypes().ClassesOnly().UsingType(allTypesToSearch).ToList();

            Dump(Filter(app, allUsages, "Controller$"), "UI");
            Dump(Filter(app, allUsages, "Page$"), "UI");
            Dump(Filter(app, allUsages, "Service$"), "Service");
            Dump(Filter(app, allUsages, "Repository$"), "Repository");
            Dump(Filter(app, allUsages, "Report$"), "Report");
            Dump(Filter(app, allUsages, "Activity$"), "Activity");
        }

        private static void MainFXDB(string[] args)
        {
            var path = @"C:\Work\Monex\fxdb2\src\FXDB2.Web\bin";
            var assemblies = ScanDirectory(path, "*.dll;*.exe");
            var app = assemblies.ToList();

            var dbContextTypes = app.GetTypes("FXDB2Context$").ToList();
            var repoTypes = app.GetTypes("Repository$").ToList();
            var allTypesToSearch = repoTypes.Concat(dbContextTypes);
            var allUsages = app.GetTypes().ClassesOnly().UsingType(allTypesToSearch).ToList();

            Dump(Filter(app, allUsages, "Controller$"), "UI");
            Dump(Filter(app, allUsages, "Page$"), "UI");
            Dump(Filter(app, allUsages, "Service$"), "Service");
            Dump(Filter(app, allUsages, "Repository$"), "Repository");
            Dump(Filter(app, allUsages, "Report$"), "Report");
            Dump(Filter(app, allUsages, "Activity$"), "Activity");
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
            var readerParameters = new ReaderParameters { AssemblyResolver = resolver };
            var assemblies = new List<AssemblyDefinition>();
            AppendDirectory(assemblies, path, mask, readerParameters);
            return assemblies;
        }

        private static void AppendDirectory(ICollection<AssemblyDefinition> assemblies, string path, string mask,
            ReaderParameters param)
        {
            var masks = mask?.Split(';') ?? new[] { string.Empty };

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
    }
    */
}