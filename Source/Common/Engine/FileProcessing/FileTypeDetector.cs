using System;
using Glasswall.Core.Engine.Common.FileProcessing;
using Glasswall.Core.Engine.Common.GlasswallEngineLibrary;
using Glasswall.Core.Engine.Messaging;
using Microsoft.Extensions.Logging;

namespace Glasswall.Core.Engine.FileProcessing
{
    public class FileTypeDetector : IFileTypeDetector
    {
        private readonly IGlasswallFileOperations _glasswallFileOperations;

        public FileTypeDetector(IGlasswallFileOperations glasswallFileOperations)
        {
            _glasswallFileOperations = glasswallFileOperations ?? throw new ArgumentNullException(nameof(glasswallFileOperations));
        }

        public FileTypeDetectionResponse DetermineFileType(byte[] fileBytes)
        {
            if (fileBytes == null) throw new ArgumentNullException(nameof(fileBytes));
            var fileType = _glasswallFileOperations.DetermineFileType(fileBytes);
            if (!Enum.IsDefined(typeof(FileType), fileType))
            {
                fileType = FileType.Unknown;
            }

            return new FileTypeDetectionResponse(fileType);
        }
    }
}