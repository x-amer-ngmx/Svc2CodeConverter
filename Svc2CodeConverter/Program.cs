using System;
using System.CodeDom;
using System.Configuration;
using System.IO;
using System.Security.AccessControl;
using FluentNHibernate.Utils;
using NHibernate.Type;

namespace Svc2CodeConverter
{
    class Program
    {
        private static readonly string ContractsDestinationPath =
            ConfigurationManager.AppSettings["contracts_destination_path"] ?? @"C:\INTEGRATION\Svc2CodeCvtResult";

        private static readonly bool ISPath = ConfigurationManager.AppSettings["current_platform"] == "patch";

        private static readonly string DtosDestinationPath = ConfigurationManager.AppSettings["dtos_destination_path"] ?? @"C:\INTEGRATION\Svc2CodeCvtResult\Dtos";

        public static readonly string[] ServicesNames = {
            "https://api.dom.gosuslugi.ru/ext-bus-org-registry-service/services/OrgRegistry",
            "https://api.dom.gosuslugi.ru/ext-bus-org-registry-service/services/OrgRegistryAsync",
            "https://api.dom.gosuslugi.ru/ext-bus-nsi-common-service/services/NsiCommon",
            "https://api.dom.gosuslugi.ru/ext-bus-nsi-common-service/services/NsiCommonAsync",
            "https://api.dom.gosuslugi.ru/ext-bus-home-management-service/services/HomeManagement",
            "https://api.dom.gosuslugi.ru/ext-bus-home-management-service/services/HomeManagementAsync",
            "https://api.dom.gosuslugi.ru/ext-bus-bills-service/services/Bills",
            "https://api.dom.gosuslugi.ru/ext-bus-bills-service/services/BillsAsync",
            "https://api.dom.gosuslugi.ru/ext-bus-device-metering-service/services/DeviceMetering",
            "https://api.dom.gosuslugi.ru/ext-bus-device-metering-service/services/DeviceMeteringAsync",
            "https://api.dom.gosuslugi.ru/ext-bus-nsi-service/services/Nsi",
            "https://api.dom.gosuslugi.ru/ext-bus-nsi-service/services/NsiAsync",
            "https://api.dom.gosuslugi.ru/ext-bus-rki-service/services/Infrastructure",
            "https://api.dom.gosuslugi.ru/ext-bus-rki-service/services/InfrastructureAsync",
            "https://api.dom.gosuslugi.ru/ext-bus-fas-service/services/FASAsync",
            "https://api.dom.gosuslugi.ru/ext-bus-payment-service/services/PaymentAsync"
        };

        private static readonly string[] WsdlAddress =
        {
            @"bills\hcs-bills-service.wsdl",
            @"bills\hcs-bills-service-async.wsdl",
            @"capital-repair\hcs-capital-repair-service.wsdl",
            @"capital-repair\hcs-capital-repair-service-async.wsdl",
            @"device-metering\hcs-device-metering-service.wsdl",
            @"device-metering\hcs-device-metering-service-async.wsdl",
            @"fas\hcs-fas-service-async.wsdl",
            @"house-management\hcs-house-management-service.wsdl",
            @"house-management\hcs-house-management-service-async.wsdl",
            @"infrastructure\hcs-infrastructure-service.wsdl",
            @"infrastructure\hcs-infrastructure-service-async.wsdl",
            @"nsi\hcs-nsi-service.wsdl",
            @"nsi\hcs-nsi-service-async.wsdl",
            @"nsi-common\hcs-nsi-common-service.wsdl",
            @"nsi-common\hcs-nsi-common-service-async.wsdl",
            @"organizations-registry\hcs-organizations-registry-service.wsdl",
            @"organizations-registry\hcs-organizations-registry-service-async.wsdl",
            @"payment\hcs-payment-service-async.wsdl"
        };

        static void Main(string[] args)
        {
            if (Directory.Exists(DtosDestinationPath))
                Directory.Delete(DtosDestinationPath, true);

            /*if (Directory.Exists(ContractsDestinationPath))
                Directory.Delete(ContractsDestinationPath, true);*/

            var unitsData = Library.LoadSvcData(ISPath ? WsdlAddress : ServicesNames, "Integration.");

            var formattedUnits = Library.GenerateCodeUnits(unitsData);

            if (!Directory.Exists(ContractsDestinationPath))
            {
                Directory.CreateDirectory(ContractsDestinationPath);
                Directory.SetAccessControl(ContractsDestinationPath, new DirectorySecurity());
            }

            Directory.CreateDirectory(DtosDestinationPath);
            Directory.SetAccessControl(DtosDestinationPath, new DirectorySecurity());

            var mappedUnits = Library.MapUnits(formattedUnits.DeepClone());

            Library.CreateServiceSupportWithUnits(mappedUnits, DtosDestinationPath, true);

            Library.CreateServiceSupportWithUnits(formattedUnits, ContractsDestinationPath);
            Console.WriteLine(@"---------------------------------Done!");
            Console.ReadKey();
        }
    }
}
