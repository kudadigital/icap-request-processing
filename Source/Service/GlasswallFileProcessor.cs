using Glasswall.Core.Engine.Common.FileProcessing;
using Glasswall.Core.Engine.Common.PolicyConfig;
using Glasswall.Core.Engine.Messaging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.IO;
using System.Text;

namespace Service
{
    public class GlasswallFileProcessor : IGlasswallFileProcessor
    {
        private readonly IGlasswallVersionService _glasswallVersionService;
        private readonly IFileTypeDetector _fileTypeDetector;
        private readonly IFileProtector _fileProtector;
        private readonly IConfigurationRoot _configuration;

        public GlasswallFileProcessor(IGlasswallVersionService glasswallVersionService, IFileTypeDetector fileTypeDetector, IFileProtector fileProtector, IConfigurationRoot configuration)
        {
            _glasswallVersionService = glasswallVersionService ?? throw new ArgumentNullException(nameof(glasswallVersionService));
            _fileTypeDetector = fileTypeDetector ?? throw new ArgumentNullException(nameof(fileTypeDetector));
            _fileProtector = fileProtector ?? throw new ArgumentNullException(nameof(fileProtector));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public void ProcessFile()
        {
            var status = RebuildFile();
            SendMessage(status);
        }

        private string RebuildFile()
        {
            byte[] protectedFile = null;

            Console.WriteLine($"Using Glasswall Version: {_glasswallVersionService.GetVersion()}");

            var file = File.ReadAllBytes(_configuration["INPUT_PATH"]);

            var fileType = _fileTypeDetector.DetermineFileType(file);

            Console.WriteLine($"Filetype Detected: {fileType.FileTypeName}");

            string status;
            if (fileType.FileType == FileType.Unknown)
            {
                status = FileOutcome.Unmodified;
            }
            else
            {
                var protectedFileResponse = _fileProtector.GetProtectedFile(GetDefaultContentManagement(), fileType.FileTypeName, file);

                if (!string.IsNullOrWhiteSpace(protectedFileResponse.ErrorMessage))
                {
                    if (protectedFileResponse.IsDisallowed)
                    {
                        status = FileOutcome.Unmodified;
                    }
                    else
                    {
                        status = FileOutcome.Failed;
                    }
                }
                else
                {
                    protectedFile = protectedFileResponse.ProtectedFile;
                    status = FileOutcome.Replace;
                }
            }

            Directory.CreateDirectory("/output");

            File.WriteAllBytes(_configuration["OUTPUT_PATH"], protectedFile ?? file);

            Console.WriteLine($"Status of: {status}");

            return status;
        }

        private void SendMessage(string status)
        {
            var factory = new ConnectionFactory() { HostName = "rabbitmq-service" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare("adaptation-response-queue", false, false, false, null);
                Console.WriteLine("Created Queue");

                var response = new AdaptationResponse
                {
                    FileId = _configuration["FILE_ID"],
                    FileOutcome = status
                };

                var message = JsonConvert.SerializeObject(response);
                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish("adaptation-exchange", "adaptation-response", null, body);
                Console.WriteLine($"Sent Message: {message}");
            };
        }

        private ContentManagementFlags GetDefaultContentManagement()
        {
            return new ContentManagementFlags
            {
                ExcelContentManagement = new ExcelContentManagement
                {
                    DynamicDataExchange = ContentManagementFlagAction.Sanitise,
                    EmbeddedFiles = ContentManagementFlagAction.Sanitise,
                    EmbeddedImages = ContentManagementFlagAction.Sanitise,
                    ExternalHyperlinks = ContentManagementFlagAction.Sanitise,
                    InternalHyperlinks = ContentManagementFlagAction.Sanitise,
                    Macros = ContentManagementFlagAction.Sanitise,
                    Metadata = ContentManagementFlagAction.Sanitise,
                    ReviewComments = ContentManagementFlagAction.Sanitise
                },
                PdfContentManagement = new PdfContentManagement
                {
                    Acroform = ContentManagementFlagAction.Sanitise,
                    ActionsAll = ContentManagementFlagAction.Sanitise,
                    EmbeddedFiles = ContentManagementFlagAction.Sanitise,
                    EmbeddedImages = ContentManagementFlagAction.Sanitise,
                    ExternalHyperlinks = ContentManagementFlagAction.Sanitise,
                    InternalHyperlinks = ContentManagementFlagAction.Sanitise,
                    Javascript = ContentManagementFlagAction.Sanitise,
                    Metadata = ContentManagementFlagAction.Sanitise,
                    Watermark = "Glasswall Approved"
                },
                PowerPointContentManagement = new PowerPointContentManagement
                {
                    EmbeddedFiles = ContentManagementFlagAction.Sanitise,
                    EmbeddedImages = ContentManagementFlagAction.Sanitise,
                    ExternalHyperlinks = ContentManagementFlagAction.Sanitise,
                    InternalHyperlinks = ContentManagementFlagAction.Sanitise,
                    Macros = ContentManagementFlagAction.Sanitise,
                    Metadata = ContentManagementFlagAction.Sanitise,
                    ReviewComments = ContentManagementFlagAction.Sanitise
                },
                WordContentManagement = new WordContentManagement
                {
                    DynamicDataExchange = ContentManagementFlagAction.Sanitise,
                    EmbeddedFiles = ContentManagementFlagAction.Sanitise,
                    EmbeddedImages = ContentManagementFlagAction.Sanitise,
                    ExternalHyperlinks = ContentManagementFlagAction.Sanitise,
                    InternalHyperlinks = ContentManagementFlagAction.Sanitise,
                    Macros = ContentManagementFlagAction.Sanitise,
                    Metadata = ContentManagementFlagAction.Sanitise,
                    ReviewComments = ContentManagementFlagAction.Sanitise
                }
            };
        }
    }
}
