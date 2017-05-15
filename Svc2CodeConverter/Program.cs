using System;
using System.CodeDom;
using System.Configuration;
using System.IO;
using System.Security.AccessControl;
using FluentNHibernate.Utils;
using NHibernate.Type;
using Svc2CodeConverter.CodeBuilder;

namespace Svc2CodeConverter
{
    class Program
    {
        private static readonly string ContractsDestinationPath =
            ConfigurationManager.AppSettings["contracts_destination_path"] ?? @"C:\INTEGRATION\Svc2CodeCvtResult";

        private static readonly bool ISPath = ConfigurationManager.AppSettings["current_platform"] == "patch";

        private static readonly string DtosDestinationPath = ConfigurationManager.AppSettings["dtos_destination_path"] ?? @"C:\INTEGRATION\Svc2CodeCvtResult\Dtos";


        static void Main(string[] args)
        {
            GlobalGonfig.InitWsdlConfig();

            if (Directory.Exists(DtosDestinationPath))
                Directory.Delete(DtosDestinationPath, true);

            /*if (Directory.Exists(ContractsDestinationPath))
                Directory.Delete(ContractsDestinationPath, true);*/

            var unitsData = GenerateBuild.LoadSvcData(GlobalGonfig.EndPointAddress, "Integration.");

            var formattedUnits = GenerateBuild.GenerateCodeUnits(unitsData);

            if (!Directory.Exists(ContractsDestinationPath))
            {
                Directory.CreateDirectory(ContractsDestinationPath);
                Directory.SetAccessControl(ContractsDestinationPath, new DirectorySecurity());
            }

            Directory.CreateDirectory(DtosDestinationPath);
            Directory.SetAccessControl(DtosDestinationPath, new DirectorySecurity());

            var mappedUnits = GenerateBuild.MapUnits(formattedUnits.DeepClone());

            GenerateBuild.CreateServiceSupportWithUnits(mappedUnits, DtosDestinationPath, true);

            GenerateBuild.CreateServiceSupportWithUnits(formattedUnits, ContractsDestinationPath);
            Console.WriteLine(@"---------------------------------Done!");
            Console.ReadKey();
        }
    }
}
