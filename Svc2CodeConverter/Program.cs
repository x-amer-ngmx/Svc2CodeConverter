using System;
using System.Configuration;
using System.IO;
using System.Security.AccessControl;
using FluentNHibernate.Utils;
using Svc2CodeConverter.CodeBuilder;

namespace Svc2CodeConverter
{
    class Program
    {

        static void Main(string[] args)
        {
            
            if (Directory.Exists(GlobalConfig.DtosDestinationPath))
                Directory.Delete(GlobalConfig.DtosDestinationPath, true);

            var unitsData = GenerateBuild.LoadSvcData(GlobalConfig.EndPointAddress, "Integration.");

            var formattedUnits = GenerateBuild.GenerateCodeUnits(unitsData);

            if (!Directory.Exists(GlobalConfig.ContractsDestinationPath))
            {
                Directory.CreateDirectory(GlobalConfig.ContractsDestinationPath);
                Directory.SetAccessControl(GlobalConfig.ContractsDestinationPath, new DirectorySecurity());
            }

            Directory.CreateDirectory(GlobalConfig.DtosDestinationPath);
            Directory.SetAccessControl(GlobalConfig.DtosDestinationPath, new DirectorySecurity());

            var mappedUnits = GenerateBuild.MapUnits(formattedUnits.DeepClone());

            GenerateBuild.CreateServiceSupportWithUnits(mappedUnits, GlobalConfig.DtosDestinationPath, true);

            GenerateBuild.CreateServiceSupportWithUnits(formattedUnits, GlobalConfig.ContractsDestinationPath);
            Console.WriteLine(@"---------------------------------Done!");
            Console.ReadKey();
        }
    }
}
