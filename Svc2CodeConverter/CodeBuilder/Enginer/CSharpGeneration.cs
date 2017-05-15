using FluentNHibernate.Utils;
using Newtonsoft.Json;
using NHibernate.Util;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading.Tasks;
using System.Xml;

namespace Svc2CodeConverter.CodeBuilder.Enginer
{
    public class CSharpGeneration : WsdlExstender
    {

        public static CodeCompileUnit[] LoadSvcData(string[] serviceEndpoints, string globalNamespaceName)
        {
            var concurrentDic = new ConcurrentDictionary<string, CodeCompileUnit>();
            //var logWriter = new IndentedTextWriter(new StreamWriter(@"c:\\log2.txt"));

            foreach (var serviceEndpoint in serviceEndpoints)
            {
                Console.WriteLine($"{serviceEndpoint} start");
                MetadataSet metadataSet = null;
                Task<MetadataSet> mexClientData = null;
                MetadataExchangeClient mexClient = null;


                    var serviceUri =
                        new Uri(serviceEndpoint);

                    var isHttps = 0 == serviceUri.ToString().IndexOf("https://", StringComparison.Ordinal);
                    var basicHttpBinding = new BasicHttpBinding
                    {
                        MaxReceivedMessageSize = int.MaxValue,
                        MaxBufferSize = int.MaxValue,
                        OpenTimeout = new TimeSpan(0, 0, 10, 0),
                        SendTimeout = new TimeSpan(0, 0, 10, 0),
                        ReceiveTimeout = new TimeSpan(0, 0, 10, 0),
                        CloseTimeout = new TimeSpan(0, 0, 10, 0),
                        AllowCookies = false,
                        ReaderQuotas = new XmlDictionaryReaderQuotas
                        {
                            MaxNameTableCharCount = int.MaxValue,
                            MaxStringContentLength = int.MaxValue,
                            MaxArrayLength = 32768,
                            MaxBytesPerRead = 4096,
                            MaxDepth = 32
                        },
                        Security =
                        {
                            Mode = isHttps ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.None,
                            Transport =
                            {
                                ClientCredentialType =
                                    isHttps ? HttpClientCredentialType.Certificate : HttpClientCredentialType.Basic
                            }
                        }
                    };

                    mexClient =
                        new MetadataExchangeClient(basicHttpBinding)
                        {
                            MaximumResolvedReferences = 1000,
                            HttpCredentials = new NetworkCredential(GlobalGonfig.SitLogin, GlobalGonfig.SitPassword),
                            ResolveMetadataReferences = true
                        };
                    mexClientData = mexClient.GetMetadataAsync(serviceUri, MetadataExchangeClientMode.HttpGet);

                do
                {
                    try
                    {
                        mexClientData.Wait();
                        metadataSet = mexClientData.Result;
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine(exc.Message);
                        //Console.WriteLine(serviceUri.ToString());
                    }

                    //System.Threading.Thread.Sleep(1000);
                } while (mexClientData == null || metadataSet == null);

                object dataContractImporter;
                XsdDataContractImporter xsdDcImporter;
                var options = new ImportOptions();

                var wsdl = new WsdlImporter(metadataSet);
                if (!wsdl.State.TryGetValue(typeof(XsdDataContractImporter), out dataContractImporter))
                {
                    xsdDcImporter = new XsdDataContractImporter { Options = options };
                    wsdl.State.Add(typeof(XsdDataContractImporter), xsdDcImporter);
                }
                else
                {
                    xsdDcImporter = (XsdDataContractImporter)dataContractImporter;
                    if (xsdDcImporter.Options == null)
                    {
                        xsdDcImporter.Options = options;
                    }
                }

                //IEnumerable<IWsdlImportExtension> exts = wsdl.WsdlImportExtensions;
                var newExts = new List<IWsdlImportExtension>();

                newExts.AddRange(wsdl.WsdlImportExtensions);

                newExts.Add(new WsdlDocumentationImporter());
                IEnumerable<IPolicyImportExtension> polExts = wsdl.PolicyImportExtensions;

                wsdl = new WsdlImporter(metadataSet, polExts, newExts);

                var contracts = wsdl.ImportAllContracts();
                wsdl.ImportAllEndpoints();
                wsdl.ImportAllBindings();
                var generator = new ServiceContractGenerator();

                foreach (var contract in contracts)
                {
                    generator.GenerateServiceContractType(contract);

                    var nsname = GetHumanString(contract.Namespace);
                    generator.TargetCompileUnit.UserData.Add("ModuleName", nsname);
                    generator.TargetCompileUnit.UserData.Add("NamespaceName", globalNamespaceName.TrimEnd('.') + '.');
                    concurrentDic.TryAdd(nsname, generator.TargetCompileUnit);
                    //logWriter.WriteLine(nsname);
                }

                Console.WriteLine($"{serviceEndpoint} end");
            }

            //logWriter.Flush(); logWriter.Close();
            return concurrentDic.Select(t => t.Value).ToArray();
        }

        public static CodeCompileUnit[] GenerateCodeUnits(CodeCompileUnit[] units)
        {
            var allTypes = new Dictionary<string, List<string>>();

            var codeExportUnits = new Dictionary<string, CodeCompileUnit>();

            var importSettings = File.ReadAllText(@"c:\\imports.json");
            var imports = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(importSettings);

            foreach (var unit in units)
            {
                foreach (var unitNamespace in unit.Namespaces.Cast<CodeNamespace>())
                {
                    foreach (var type in unitNamespace.Types.Cast<CodeTypeDeclaration>()) //- рабочая версия
                    //foreach (var type in unitNamespace.Types.Cast<CodeTypeDeclaration>().Where(t => t.Name.Equals("RequestHeader")))//Версия для теста
                    {
                        if (type.IsEnum && type.Name.StartsWith("Item")) continue;

                        ProcessType(type);

                        var nsname = GetNamespaceFromAttributes(new KeyValuePair<string, CodeTypeDeclaration>(unit.UserData["ModuleName"] + "." + type.Name, type));
                        type.UserData["FullTypeName"] = unit.UserData["NamespaceName"] + nsname + '.' + type.Name;

                        if (!codeExportUnits.ContainsKey(nsname))
                        {
                            var codeNamespace = new CodeNamespace("");
                            if (imports.ContainsKey(nsname))
                                codeNamespace.Imports.AddRange(imports[nsname].Select(t => new CodeNamespaceImport(t)).ToArray());
                            allTypes.Add(nsname, new List<string>());
                            codeExportUnits.Add(nsname, new CodeCompileUnit
                            {
                                Namespaces = { codeNamespace, new CodeNamespace(unit.UserData["NamespaceName"] + nsname) },
                                UserData = { { "ModuleName", nsname }, { "NamespaceName", unit.UserData["NamespaceName"] } }//Не типы сервисов, а общие типы, которые присутствуют в модулях и у них одно и то же имя
                            });
                        }

                        if (allTypes[nsname].Contains(type.Name)) continue;
                        allTypes[nsname].Add(type.Name);
                        codeExportUnits[nsname].Namespaces[1].Types.Add(type);

                        //logWriter.WriteLine(nsname + " " + unit.UserData["ModuleName"] + "." + type.Name);
                    }
                }
            }

            //logWriter.Flush(); logWriter.Close();

            return codeExportUnits.Select(t => t.Value).ToArray();
        }

        /// <summary>
        /// Создаёт файловое окружение для сервисов
        /// </summary>
        /// <param name="units"></param>
        /// <param name="path"></param>
        /// <param name="isVirtual"></param>
        public static void CreateServiceSupportWithUnits(CodeCompileUnit[] units, string path, bool isVirtual = false)
        {
            var codeDomProvider = CodeDomProvider.CreateProvider("C#");

            foreach (var unit in units)
            {
                var exportFileName = unit.UserData["ModuleName"].ToString().Split('.').Last() + ".cs";
                using (var myTextWriter = new AbstractIndentedTextWriter
                    (new StreamWriter(path.TrimEnd('\\') + '\\' + exportFileName), isVirtual: isVirtual))
                {
                    codeDomProvider.GenerateCodeFromCompileUnit(unit, myTextWriter, GetOptions);
                    myTextWriter.Flush(); myTextWriter.Close();
                }
            }
        }

        static Dictionary<string, CodeTypeDeclaration> map2Types_Level_One(CodeCompileUnit[] mappedUnits)
        {
            var mapperTypes = new Dictionary<string, CodeTypeDeclaration>();
            foreach (var mapUnit in mappedUnits)
            {
                foreach (var nsType in mapUnit.Namespaces[1].Types.Cast<CodeTypeDeclaration>())
                {
                    var bType = nsType.UserData["FullTypeName"].ToString();

                    if (!mapperTypes.ContainsKey(bType))
                    {
                        if (!nsType.UserData.Contains(bType))
                        {
                            var ns = mapUnit.UserData["NamespaceName"].ToString();
                            nsType.UserData.Add("FunctionName", bType.Replace(ns, ""));
                            nsType.UserData.Add("NamespaceName", ns.TrimEnd('.'));
                        }
                        mapperTypes.Add(bType, nsType);
                    }
                }
            }

            return mapperTypes;
        }

        public static CodeCompileUnit[] MapUnits(CodeCompileUnit[] mappedUnits)
        {
            var types = map2Types_Level_One(mappedUnits);
            CvtAndSave2Json(types, @"C:\scheme.json");

            var newMappingUnits = new List<CodeCompileUnit>();

            foreach (var ns in mappedUnits
                .Where(t => false == t.UserData["ModuleName"].ToString().Equals("Xmldsig")).Select(t => t.Namespaces))
            {
                var typesFromNamespace = ns[1].Types.Cast<CodeTypeDeclaration>()
                    .Where(t => t.IsClass &&
                    !t.CustomAttributes.Cast<CodeAttributeDeclaration>().Any(ca => ca.Name.Equals("System.ServiceModel.MessageContractAttribute")) &&
                    !(t.BaseTypes.Cast<CodeTypeReference>().Any(ctr => ctr.BaseType.Equals("HeaderType") ||
                        ctr.BaseType.IndexOf("System.ServiceModel.ClientBase", StringComparison.Ordinal) == 0)) &&
                        !t.Name.Equals("HeaderType") &&
                        !t.Name.Equals("BaseType") &&
                        !t.Name.Equals("Fault") && t.IsClass
                    ).ToArray();

                if (!typesFromNamespace.Any()) continue;

                var newNs = new CodeNamespace(ns[1].Name + "Dto");
                var nhMappedTypes = SetNhibernateMapping(typesFromNamespace.DeepClone(), types);
                newNs.Types.AddRange(nhMappedTypes);

                newMappingUnits.Add(new CodeCompileUnit
                {
                    Namespaces = { new CodeNamespace(), newNs },
                    UserData = { { "ModuleName", ns[1].Name + "Dto" } }
                });

            }

            return newMappingUnits.ToArray();
        }

        private static void CvtAndSave2Json(Dictionary<string, CodeTypeDeclaration> map2TypesLevelOne, string fileName)
        {
            var jsonObj = JsonConvert.SerializeObject(map2TypesLevelOne);//Prepare

            using (var jsonWriter = new JsonTextWriter(new StreamWriter(fileName)))
            {
                jsonWriter.WriteRaw(jsonObj);
                jsonWriter.Flush(); jsonWriter.Close();
            }
        }

        private static CodeTypeDeclaration[] SetNhibernateMapping(CodeTypeDeclaration[] deepClone, Dictionary<string, CodeTypeDeclaration> types)
        {
            if (!deepClone.Any())
                return deepClone;

            var newTypes = new List<CodeTypeDeclaration>();
            var changedTypes = Change2DtoClass(deepClone, types);
            newTypes.AddRange(changedTypes);//Changes in class
            newTypes.AddRange(Change2DtoMapClass(changedTypes.DeepClone(), types));//FluentNHMapping declaration

            return newTypes.ToArray();
        }

        /// <summary>
        /// Изменяет описание класса - максимально приближённому к описанию DTO объекта
        /// </summary>
        /// <param name="deepClone"></param>
        /// <returns></returns>
        private static CodeTypeDeclaration[] Change2DtoClass(CodeTypeDeclaration[] deepClone, Dictionary<string, CodeTypeDeclaration> types)
        {
            foreach (var t in deepClone)
            {
                t.CustomAttributes.Clear();
                t.Name += "Dto";

                foreach (var tMember in t.Members.OfType<CodeMemberProperty>())
                {
                    tMember.CustomAttributes.Clear();
                    //Проверяю, что тип - не перечисление, а класс
                    if (!types.Any(tm => tm.Value.Name.Equals(tMember.Type.BaseType) && tm.Value.IsClass)) continue;
                    if (tMember.Type.ArrayRank > 0)
                    {
                        var bt = tMember.Type.BaseType;
                        tMember.Type.ArrayRank = 0;
                        tMember.Type.ArrayElementType = null;
                        tMember.Type.BaseType = bt + "Dto";
                        tMember.Type = tMember.Type.BaseType.Equals("System.String") ? tMember.Type : new CodeTypeReference("IList", tMember.Type);
                        continue;
                    }

                    tMember.Type.BaseType += "Dto";
                }

                if (t.BaseTypes.Any() && types.Any(tt => tt.Value.Name.Equals(t.BaseTypes.Cast<CodeTypeReference>().First().BaseType)))
                {
                    var bt = t.BaseTypes.Cast<CodeTypeReference>().First();

                    bt.BaseType += "Dto";
                    bt.BaseType = bt.BaseType.Replace("BaseType", "Entity");
                    continue;
                }

                t.BaseTypes.Add(new CodeTypeReference("EntityDto"));
            }

            return deepClone.Where(dc => dc.Name.Contains("Dto")).ToArray();
        }

        /// <summary>
        /// Генерирует мапинг на изменённый класс
        /// </summary>
        /// <param name="deepClone"></param>
        /// <returns></returns>
        private static CodeTypeDeclaration[] Change2DtoMapClass(CodeTypeDeclaration[] deepClone, Dictionary<string, CodeTypeDeclaration> types)
        {
            foreach (var t in types.Values.Where(tv => deepClone.Any(c => tv.Name + "Dto" == c.Name)))//deepClone.Select(p => types.Where(m => m.Value.Name.Equals(p.Name.Replace("Dto", "")))))
            {
                var theType = deepClone.First(p => p.Name == t.Name + "Dto");
                theType.CustomAttributes.Clear();

                //t.Members.Clear();
                theType.Attributes = MemberAttributes.Public;
                theType.BaseTypes.Clear();
                theType.BaseTypes.Add(new CodeTypeReference("MapAction", new CodeTypeReference(t.Name + "Dto")));
                theType.Name += "Map";

                var param = t.UserData["FunctionName"].ToString().Split('.');

                var ctor = new CodeConstructor
                {
                    Attributes = MemberAttributes.Public,
                    BaseConstructorArgs =
                    {
                        new CodeArgumentReferenceExpression(
                            $"\"{param.First()}\""),
                        new CodeArgumentReferenceExpression(
                            $"\"{param.Last()}\""),
                        new CodeArgumentReferenceExpression("id => id.Id")
                    }
                };

                foreach (var member in theType.Members.OfType<CodeMemberProperty>())
                {
                    var isMap = types.Where(p => (p.Key + " ").Contains('.' + member.Type.BaseType + " ")).Any(pt => pt.Value.IsEnum);

                    if (member.Type.BaseType.IndexOf("System.", StringComparison.Ordinal) == 0 || isMap)//CommonLanguageRuntimeType
                    {
                        var mapTypeExpression = new CodeMethodInvokeExpression(null, "Map",
                            new CodeArgumentReferenceExpression($"map => map.{member.Name}"));
                        var customTypeExpression = new CodeMethodInvokeExpression(mapTypeExpression, "CustomType<int>");//Добавление преобразования типа при SByte
                        ctor.Statements.Add(new CodeExpressionStatement(member.Type.BaseType.Equals("System.SByte") ? customTypeExpression : mapTypeExpression));

                        continue;
                    }

                    if (member.Type.BaseType.Contains("IList"))//IList - SetThisColumnKey(HasMany(j => j.[PropName]).Cascade.All());
                    {
                        ctor.Statements.Add(
                            new CodeExpressionStatement(
                                new CodeMethodInvokeExpression(null, "SetThisColumnKey", new CodeArgumentReferenceExpression($"HasMany(hm => hm.{member.Name}).Cascade.All()"))));
                        continue;
                    }

                    ctor.Statements.Add(
                        new CodeExpressionStatement(
                            new CodeMethodInvokeExpression(null, "References", new CodeArgumentReferenceExpression($"r => r.{member.Name}"))));
                }

                theType.Members.Clear();
                theType.Members.Add(ctor);

            }

            return deepClone;
        }

        /// <summary>
        /// Генерируем уже маппинг для выбранного класса
        /// </summary>
        /// <param name="sampleType"></param>
        /// <returns></returns>
        private static CodeTypeDeclaration GenerateMappingForGivenType(CodeTypeDeclaration sampleType)
        {
            return new CodeTypeDeclaration();
        }
    }
}
